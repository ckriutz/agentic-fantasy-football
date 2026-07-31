using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Azure;
using Azure.Storage.Blobs;
using LeagueAPI.Data;
using LeagueAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace LeagueAPI.Services;

public sealed class FantasyProsPointsImportService(BlobServiceClient blobServiceClient, IDbContextFactory<LeagueApiDbContext> dbContextFactory, ILogger<FantasyProsPointsImportService> logger)
{
    private const string StartedStatus = "Started";
    private const string SucceededStatus = "Succeeded";
    private const string FailedStatus = "Failed";
    private const string ExpectedScoring = "PPR";
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly BlobServiceClient _blobServiceClient = blobServiceClient;
    private readonly IDbContextFactory<LeagueApiDbContext> _dbContextFactory = dbContextFactory;
    private readonly ILogger<FantasyProsPointsImportService> _logger = logger;
    private readonly SemaphoreSlim _syncLock = new(1, 1);

    public async Task<FantasyProsScoreSyncRun?> GetLatestSyncRunAsync(int? season, CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var query = dbContext.FantasyProsScoreSyncRuns.AsNoTracking();
        if (season is int seasonFilter)
        {
            query = query.Where(run => run.Season == seasonFilter);
        }

        return await query
            .OrderByDescending(run => run.StartedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<FantasyProsScoreSyncRun> ImportAsync(FantasyProsPointsImportRequest request, CancellationToken cancellationToken)
    {
        request = ValidateRequest(request);
        await _syncLock.WaitAsync(cancellationToken);

        try
        {
            var syncRun = new FantasyProsScoreSyncRun
            {
                SyncRunId = Guid.NewGuid(),
                ContainerName = request.ContainerName,
                BlobName = request.BlobName,
                Season = request.RequestedSeason,
                EndWeek = request.EndWeek,
                RetrievedAtUtc = request.RetrievedAtUtc,
                ServedSeason = request.ServedSeason,
                ServedScoring = request.ServedScoring,
                StartedAtUtc = DateTimeOffset.UtcNow,
                Status = StartedStatus
            };

            await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            dbContext.FantasyProsScoreSyncRuns.Add(syncRun);
            await dbContext.SaveChangesAsync(cancellationToken);

            try
            {
                if (TryGetStaleGuardSkipReason(request, out var skipReason))
                {
                    syncRun.CompletedAtUtc = DateTimeOffset.UtcNow;
                    syncRun.Status = SucceededStatus;
                    syncRun.AlreadyProcessed = true;
                    syncRun.RecordCount = 0;
                    syncRun.MatchedPlayerCount = 0;
                    syncRun.UnmatchedPlayerCount = 0;
                    syncRun.UnmatchedDstCount = 0;
                    syncRun.ErrorMessage = skipReason;
                    await dbContext.SaveChangesAsync(cancellationToken);

                    _logger.LogInformation(
                        "FantasyPros points import {SyncRunId} skipped for {ContainerName}/{BlobName}: {SkipReason}",
                        syncRun.SyncRunId,
                        request.ContainerName,
                        request.BlobName,
                        skipReason);

                    return syncRun;
                }

                var blobClient = _blobServiceClient.GetBlobContainerClient(request.ContainerName).GetBlobClient(request.BlobName);
                var download = await blobClient.DownloadContentAsync(cancellationToken);
                var content = download.Value.Content.ToMemory();
                var contentHash = Convert.ToHexString(SHA256.HashData(content.Span));

                syncRun.BlobETag = download.Value.Details.ETag.ToString();
                syncRun.ContentHash = contentHash;

                var snapshot = JsonSerializer.Deserialize<FantasyProsPointsSnapshot>(content.Span, SerializerOptions)
                    ?? throw new InvalidDataException("The FantasyPros points blob does not contain a valid snapshot.");
                ValidateSnapshot(snapshot, request);

                // Dedupe on content hash + container + blob + season + end week (exclude RetrievedAtUtc — points sync every 15 minutes).
                var alreadyProcessed = await dbContext.FantasyProsScoreSyncRuns.AsNoTracking()
                    .AnyAsync(run =>
                        run.SyncRunId != syncRun.SyncRunId
                        && run.Status == SucceededStatus
                        && run.ContainerName == request.ContainerName
                        && run.BlobName == request.BlobName
                        && run.Season == request.RequestedSeason
                        && run.EndWeek == request.EndWeek
                        && run.ContentHash == contentHash,
                        cancellationToken);

                var matchedPlayerCount = 0;
                var unmatchedPlayerCount = 0;
                var unmatchedDstCount = 0;
                var recordCount = 0;

                if (!alreadyProcessed)
                {
                    var upsertResult = await UpsertWeeklyScoresAsync(dbContext, snapshot, request.RequestedSeason, syncRun.SyncRunId, cancellationToken);
                    matchedPlayerCount = upsertResult.MatchedPlayerCount;
                    unmatchedPlayerCount = upsertResult.UnmatchedPlayerCount;
                    unmatchedDstCount = upsertResult.UnmatchedDstCount;
                    recordCount = upsertResult.RecordCount;
                }

                syncRun.CompletedAtUtc = DateTimeOffset.UtcNow;
                syncRun.Status = SucceededStatus;
                syncRun.RecordCount = recordCount;
                syncRun.MatchedPlayerCount = matchedPlayerCount;
                syncRun.UnmatchedPlayerCount = unmatchedPlayerCount;
                syncRun.UnmatchedDstCount = unmatchedDstCount;
                syncRun.AlreadyProcessed = alreadyProcessed;
                syncRun.ErrorMessage = null;
                await dbContext.SaveChangesAsync(cancellationToken);

                _logger.LogInformation(
                    "FantasyPros points import {SyncRunId} succeeded for {ContainerName}/{BlobName}: {RecordCount} weekly scores, matched players: {MatchedPlayerCount}, unmatched players: {UnmatchedPlayerCount}, unmatched DSTs: {UnmatchedDstCount}, already processed: {AlreadyProcessed}.",
                    syncRun.SyncRunId,
                    request.ContainerName,
                    request.BlobName,
                    recordCount,
                    matchedPlayerCount,
                    unmatchedPlayerCount,
                    unmatchedDstCount,
                    alreadyProcessed);

                return syncRun;
            }
            catch (OperationCanceledException)
            {
                await MarkFailedAsync(syncRun.SyncRunId, "The FantasyPros points import was canceled.");
                throw;
            }
            catch (Exception exception) when (exception is RequestFailedException or JsonException or InvalidDataException or DbUpdateException)
            {
                await MarkFailedAsync(syncRun.SyncRunId, exception.Message);
                throw;
            }
        }
        finally
        {
            _syncLock.Release();
        }
    }

    private static FantasyProsPointsImportRequest ValidateRequest(FantasyProsPointsImportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var containerName = request.ContainerName?.Trim();
        if (string.IsNullOrWhiteSpace(containerName))
        {
            throw new ArgumentException("ContainerName is required.", nameof(request));
        }

        if (containerName.Length is < 3 or > 63
            || containerName[0] == '-'
            || containerName[^1] == '-'
            || containerName.Contains("--", StringComparison.Ordinal)
            || containerName.Any(character => character is not (>= 'a' and <= 'z') and not (>= '0' and <= '9') and not '-'))
        {
            throw new ArgumentException("ContainerName must be a valid lowercase Azure Blob container name.", nameof(request));
        }

        var blobName = request.BlobName?.Trim();
        if (string.IsNullOrWhiteSpace(blobName))
        {
            throw new ArgumentException("BlobName is required.", nameof(request));
        }

        if (blobName.Length > 1024 || blobName.Any(char.IsControl))
        {
            throw new ArgumentException("BlobName must be at most 1024 characters and cannot contain control characters.", nameof(request));
        }

        if (request.RequestedSeason is < 2000 or > 2100)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "RequestedSeason must be between 2000 and 2100.");
        }

        if (request.EndWeek is < 0 or > 18)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "EndWeek must be between 0 and 18.");
        }

        if (request.RetrievedAtUtc == default || request.RetrievedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("RetrievedAtUtc is required and must use the UTC offset.", nameof(request));
        }

        var servedSeason = request.ServedSeason?.Trim();
        if (string.IsNullOrWhiteSpace(servedSeason))
        {
            throw new ArgumentException("ServedSeason is required.", nameof(request));
        }

        var servedScoring = request.ServedScoring?.Trim();
        if (string.IsNullOrWhiteSpace(servedScoring))
        {
            throw new ArgumentException("ServedScoring is required.", nameof(request));
        }

        return request with
        {
            ContainerName = containerName,
            BlobName = blobName,
            ServedSeason = servedSeason,
            ServedScoring = servedScoring
        };
    }

    private static bool TryGetStaleGuardSkipReason(FantasyProsPointsImportRequest request, out string skipReason)
    {
        if (!int.TryParse(request.ServedSeason, NumberStyles.Integer, CultureInfo.InvariantCulture, out var servedSeasonNumber)
            || servedSeasonNumber != request.RequestedSeason)
        {
            skipReason = $"Skipped stale season payload: requested {request.RequestedSeason}, served {request.ServedSeason}.";
            return true;
        }

        if (!string.Equals(request.ServedScoring, ExpectedScoring, StringComparison.OrdinalIgnoreCase))
        {
            skipReason = $"Skipped non-PPR scoring payload: served scoring {request.ServedScoring}.";
            return true;
        }

        skipReason = string.Empty;
        return false;
    }

    private static void ValidateSnapshot(FantasyProsPointsSnapshot snapshot, FantasyProsPointsImportRequest request)
    {
        if (!string.Equals(snapshot.Season, request.ServedSeason, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Snapshot season {snapshot.Season} does not match served season {request.ServedSeason}.");
        }

        if (!string.Equals(snapshot.Scoring, request.ServedScoring, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Snapshot scoring {snapshot.Scoring} does not match served scoring {request.ServedScoring}.");
        }

        if (snapshot.RetrievedAtUtc != request.RetrievedAtUtc)
        {
            throw new InvalidDataException($"Snapshot retrieval time {snapshot.RetrievedAtUtc:O} does not match requested retrieval time {request.RetrievedAtUtc:O}.");
        }

        if (snapshot.RetrievedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new InvalidDataException("Snapshot RetrievedAtUtc must use the UTC offset.");
        }

        if (snapshot.Players is null || snapshot.Players.Count == 0)
        {
            throw new InvalidDataException("The FantasyPros points snapshot must contain at least one player.");
        }

        if (snapshot.Players.Any(player => player is null || player.PlayerId <= 0))
        {
            throw new InvalidDataException("Every FantasyPros points player must have a positive player_id.");
        }

        var duplicatePlayerId = snapshot.Players.GroupBy(player => player.PlayerId).FirstOrDefault(group => group.Count() > 1)?.Key;
        if (duplicatePlayerId is not null)
        {
            throw new InvalidDataException($"The FantasyPros points snapshot contains duplicate player_id {duplicatePlayerId}.");
        }
    }

    private static async Task<UpsertResult> UpsertWeeklyScoresAsync(LeagueApiDbContext dbContext, FantasyProsPointsSnapshot snapshot, int season, Guid syncRunId, CancellationToken cancellationToken)
    {
        var fantasyProsPlayerIds = snapshot.Players.Select(player => player.PlayerId).Distinct().ToArray();
        var rankingPlayers = await dbContext.FantasyProsRankingPlayers.AsNoTracking()
            .Where(player => fantasyProsPlayerIds.Contains(player.PlayerId))
            .Select(player => new { player.PlayerId, player.PlayerYahooId, player.SportsDataId })
            .ToListAsync(cancellationToken);

        var rankingByFantasyProsId = rankingPlayers.ToDictionary(player => player.PlayerId);

        var bridgeIdentities = snapshot.Players.Select(player =>
        {
            rankingByFantasyProsId.TryGetValue(player.PlayerId, out var ranking);
            return new FantasyProsPlayerBridge.Identity(
                ranking?.PlayerYahooId,
                ranking?.SportsDataId,
                player.PositionId,
                player.TeamId);
        });
        var lookupMaps = await FantasyProsPlayerBridge.LoadMapsAsync(dbContext, bridgeIdentities, cancellationToken);

        var scoreKeys = new List<(int Week, int FantasyProsPlayerId)>();
        var pendingScores = new List<PendingWeeklyScore>();
        var matchedPlayerIds = new HashSet<int>();
        var unmatchedPlayerIds = new HashSet<int>();
        var unmatchedDstIds = new HashSet<int>();
        var updatedAtUtc = DateTimeOffset.UtcNow;

        foreach (var player in snapshot.Players)
        {
            if (player.Weeks is null || player.Weeks.Count == 0)
            {
                continue;
            }

            rankingByFantasyProsId.TryGetValue(player.PlayerId, out var ranking);
            var identity = new FantasyProsPlayerBridge.Identity(
                ranking?.PlayerYahooId,
                ranking?.SportsDataId,
                player.PositionId,
                player.TeamId);
            var sleeperPlayerId = FantasyProsPlayerBridge.ResolveSleeperPlayerId(identity, lookupMaps);

            var wroteScoreRow = false;
            foreach (var (weekKey, points) in player.Weeks)
            {
                if (!int.TryParse(weekKey, NumberStyles.Integer, CultureInfo.InvariantCulture, out var week) || week is < 1 or > 18)
                {
                    continue;
                }

                // Null week values are treated as absent; keep explicit zeros and negatives.
                if (points is null)
                {
                    continue;
                }

                var roundedPoints = decimal.Round(points.Value, 1, MidpointRounding.AwayFromZero);
                scoreKeys.Add((week, player.PlayerId));
                pendingScores.Add(new PendingWeeklyScore(week, player.PlayerId, sleeperPlayerId, player.PlayerName, player.PositionId, player.TeamId, roundedPoints));
                wroteScoreRow = true;
            }

            // Count match quality only for players that actually produce score rows.
            if (!wroteScoreRow)
            {
                continue;
            }

            if (sleeperPlayerId is null)
            {
                unmatchedPlayerIds.Add(player.PlayerId);
                if (IsLikelyDst(player))
                {
                    unmatchedDstIds.Add(player.PlayerId);
                }
            }
            else
            {
                matchedPlayerIds.Add(player.PlayerId);
            }
        }

        var existingScores = new Dictionary<(int Week, int FantasyProsPlayerId), WeeklyPlayerScoreEntity>();
        if (scoreKeys.Count > 0)
        {
            var weeks = scoreKeys.Select(key => key.Week).Distinct().ToArray();
            var playerIds = scoreKeys.Select(key => key.FantasyProsPlayerId).Distinct().ToArray();
            var existing = await dbContext.WeeklyPlayerScores
                .Where(score => score.Season == season && weeks.Contains(score.Week) && playerIds.Contains(score.FantasyProsPlayerId))
                .ToListAsync(cancellationToken);

            foreach (var score in existing)
            {
                existingScores[(score.Week, score.FantasyProsPlayerId)] = score;
            }
        }

        foreach (var pending in pendingScores)
        {
            if (!existingScores.TryGetValue((pending.Week, pending.FantasyProsPlayerId), out var entity))
            {
                entity = new WeeklyPlayerScoreEntity
                {
                    Season = season,
                    Week = pending.Week,
                    FantasyProsPlayerId = pending.FantasyProsPlayerId
                };
                dbContext.WeeklyPlayerScores.Add(entity);
                existingScores[(pending.Week, pending.FantasyProsPlayerId)] = entity;
            }

            entity.SleeperPlayerId = pending.SleeperPlayerId;
            entity.PlayerName = pending.PlayerName;
            entity.PositionId = pending.PositionId;
            entity.TeamId = pending.TeamId;
            entity.Points = pending.Points;
            entity.SyncRunId = syncRunId;
            entity.UpdatedAtUtc = updatedAtUtc;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new UpsertResult(pendingScores.Count, matchedPlayerIds.Count, unmatchedPlayerIds.Count, unmatchedDstIds.Count);
    }

    private static bool IsLikelyDst(FantasyProsPlayerPoints player)
    {
        if (FantasyProsPlayerBridge.IsDstPosition(player.PositionId))
        {
            return true;
        }

        // FantasyPros team defenses historically use the 8000+ id range.
        return player.PlayerId is >= 8000 and < 9000;
    }

    private async Task MarkFailedAsync(Guid syncRunId, string errorMessage)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(CancellationToken.None);
        var syncRun = await dbContext.FantasyProsScoreSyncRuns.SingleAsync(run => run.SyncRunId == syncRunId, CancellationToken.None);
        syncRun.CompletedAtUtc = DateTimeOffset.UtcNow;
        syncRun.Status = FailedStatus;
        syncRun.ErrorMessage = errorMessage;
        await dbContext.SaveChangesAsync(CancellationToken.None);
        _logger.LogError("FantasyPros points import {SyncRunId} failed: {ErrorMessage}", syncRunId, errorMessage);
    }

    private sealed record PendingWeeklyScore(int Week, int FantasyProsPlayerId, string? SleeperPlayerId, string? PlayerName, string? PositionId, string? TeamId, decimal Points);

    private sealed record UpsertResult(int RecordCount, int MatchedPlayerCount, int UnmatchedPlayerCount, int UnmatchedDstCount);
}
