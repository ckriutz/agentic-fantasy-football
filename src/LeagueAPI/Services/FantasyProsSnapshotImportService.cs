using System.Security.Cryptography;
using System.Text.Json;
using Azure;
using Azure.Storage.Blobs;
using LeagueAPI.Data;
using LeagueAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace LeagueAPI.Services;

public sealed class FantasyProsSnapshotImportService(BlobServiceClient blobServiceClient, IDbContextFactory<LeagueApiDbContext> dbContextFactory, ILogger<FantasyProsSnapshotImportService> logger)
{
    private const string StartedStatus = "Started";
    private const string SucceededStatus = "Succeeded";
    private const string FailedStatus = "Failed";
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly BlobServiceClient _blobServiceClient = blobServiceClient;
    private readonly IDbContextFactory<LeagueApiDbContext> _dbContextFactory = dbContextFactory;
    private readonly ILogger<FantasyProsSnapshotImportService> _logger = logger;
    private readonly SemaphoreSlim _syncLock = new(1, 1);

    /// <summary>
    /// Imports one FantasyPros snapshot from Azure Blob Storage.
    /// </summary>
    public async Task<FantasyProsSyncRun> ImportAsync(FantasyProsSnapshotImportRequest request, CancellationToken cancellationToken)
    {
        request = ValidateRequest(request);
        await _syncLock.WaitAsync(cancellationToken);

        try
        {
            var syncRun = new FantasyProsSyncRun
            {
                SyncRunId = Guid.NewGuid(),
                ContainerName = request.ContainerName,
                BlobName = request.BlobName,
                Season = request.Season,
                Week = request.Week,
                RetrievedAtUtc = request.RetrievedAtUtc,
                StartedAtUtc = DateTimeOffset.UtcNow,
                Status = StartedStatus
            };

            await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            dbContext.FantasyProsSyncRuns.Add(syncRun);
            await dbContext.SaveChangesAsync(cancellationToken);

            try
            {
                var blobClient = _blobServiceClient.GetBlobContainerClient(request.ContainerName).GetBlobClient(request.BlobName);
                var download = await blobClient.DownloadContentAsync(cancellationToken);
                var content = download.Value.Content.ToMemory();
                var contentHash = Convert.ToHexString(SHA256.HashData(content.Span));

                syncRun.BlobETag = download.Value.Details.ETag.ToString();
                syncRun.ContentHash = contentHash;

                using var document = JsonDocument.Parse(content);
                var snapshot = document.RootElement.Deserialize<FantasyProsPlayersSnapshot>(SerializerOptions) ?? throw new InvalidDataException("The FantasyPros blob does not contain a valid snapshot.");
                ValidateSnapshot(snapshot, request);

                if (!document.RootElement.TryGetProperty("players", out var playerElements) || playerElements.ValueKind != JsonValueKind.Array)
                {
                    throw new InvalidDataException("The FantasyPros snapshot must contain a players array.");
                }

                var rawPlayerJson = playerElements.EnumerateArray().Select(player => player.GetRawText()).ToArray();
                if (rawPlayerJson.Length != snapshot.Players.Count)
                {
                    throw new InvalidDataException("The FantasyPros snapshot players could not be read consistently.");
                }

                var alreadyProcessed = await dbContext.FantasyProsSyncRuns.AsNoTracking()
                    .AnyAsync(run =>
                        run.SyncRunId != syncRun.SyncRunId
                        && run.Status == SucceededStatus
                        && run.ContainerName == request.ContainerName
                        && run.BlobName == request.BlobName
                        && run.Season == request.Season
                        && run.Week == request.Week
                        && run.RetrievedAtUtc == request.RetrievedAtUtc
                        && run.ContentHash == contentHash,
                        cancellationToken);

                if (!alreadyProcessed)
                {
                    await UpsertPlayersAsync(dbContext, snapshot, rawPlayerJson, cancellationToken);
                }

                await ApplyFantasyProsEnrichmentAsync(dbContext, snapshot.Players, cancellationToken);

                syncRun.CompletedAtUtc = DateTimeOffset.UtcNow;
                syncRun.Status = SucceededStatus;
                syncRun.RecordCount = snapshot.Players.Count;
                syncRun.AlreadyProcessed = alreadyProcessed;
                syncRun.ErrorMessage = null;
                await dbContext.SaveChangesAsync(cancellationToken);

                _logger.LogInformation(
                    "FantasyPros snapshot import {SyncRunId} succeeded for {ContainerName}/{BlobName}: {RecordCount} players, already processed: {AlreadyProcessed}.",
                    syncRun.SyncRunId,
                    request.ContainerName,
                    request.BlobName,
                    snapshot.Players.Count,
                    alreadyProcessed);

                return syncRun;
            }
            catch (OperationCanceledException)
            {
                await MarkFailedAsync(syncRun.SyncRunId, "The FantasyPros snapshot import was canceled.");
                throw;
            }
            catch (RequestFailedException exception)
            {
                await MarkFailedAsync(syncRun.SyncRunId, exception.Message);
                throw;
            }
            catch (JsonException exception)
            {
                await MarkFailedAsync(syncRun.SyncRunId, exception.Message);
                throw;
            }
            catch (InvalidDataException exception)
            {
                await MarkFailedAsync(syncRun.SyncRunId, exception.Message);
                throw;
            }
            catch (DbUpdateException exception)
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

    private static FantasyProsSnapshotImportRequest ValidateRequest(FantasyProsSnapshotImportRequest request)
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

        if (request.Season is < 2000 or > 2100)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Season must be between 2000 and 2100.");
        }

        if (request.Week is < 0 or > 18)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Week must be between 0 and 18.");
        }

        if (request.RetrievedAtUtc == default || request.RetrievedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("RetrievedAtUtc is required and must use the UTC offset.", nameof(request));
        }

        return request with { ContainerName = containerName, BlobName = blobName };
    }

    private static void ValidateSnapshot(FantasyProsPlayersSnapshot snapshot, FantasyProsSnapshotImportRequest request)
    {
        if (snapshot.Season != request.Season)
        {
            throw new InvalidDataException($"Snapshot season {snapshot.Season} does not match requested season {request.Season}.");
        }

        if (snapshot.Week != request.Week)
        {
            throw new InvalidDataException($"Snapshot week {snapshot.Week} does not match requested week {request.Week}.");
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
            throw new InvalidDataException("The FantasyPros snapshot must contain at least one player.");
        }

        if (snapshot.Players.Any(player => player is null || player.PlayerId <= 0))
        {
            throw new InvalidDataException("Every FantasyPros player must have a positive player_id.");
        }

        var duplicatePlayerId = snapshot.Players.GroupBy(player => player.PlayerId).FirstOrDefault(group => group.Count() > 1)?.Key;
        if (duplicatePlayerId is not null)
        {
            throw new InvalidDataException($"The FantasyPros snapshot contains duplicate player_id {duplicatePlayerId}.");
        }
    }

    private static async Task UpsertPlayersAsync(LeagueApiDbContext dbContext, FantasyProsPlayersSnapshot snapshot, IReadOnlyList<string> rawPlayerJson, CancellationToken cancellationToken)
    {
        var playerIds = snapshot.Players.Select(player => player.PlayerId).ToArray();
        var existingPlayersById = await dbContext.FantasyProsRankingPlayers
            .Where(player => playerIds.Contains(player.PlayerId))
            .ToDictionaryAsync(player => player.PlayerId, cancellationToken);
        var updatedAtUtc = DateTimeOffset.UtcNow;

        for (var index = 0; index < snapshot.Players.Count; index++)
        {
            var player = snapshot.Players[index];
            if (!existingPlayersById.TryGetValue(player.PlayerId, out var entity))
            {
                entity = new FantasyProsRankingPlayerEntity
                {
                    PlayerId = player.PlayerId,
                    RawJson = rawPlayerJson[index]
                };
                dbContext.FantasyProsRankingPlayers.Add(entity);
            }

            entity.PlayerName = player.PlayerName;
            entity.SportsDataId = player.SportsDataId;
            entity.PlayerTeamId = player.PlayerTeamId;
            entity.PlayerPositionId = player.PlayerPositionId;
            entity.PlayerPositions = player.PlayerPositions;
            entity.PlayerShortName = player.PlayerShortName;
            entity.PlayerEligibility = player.PlayerEligibility;
            entity.PlayerYahooPositions = player.PlayerYahooPositions;
            entity.PlayerPageUrl = player.PlayerPageUrl;
            entity.PlayerFilename = player.PlayerFilename;
            entity.PlayerYahooId = player.PlayerYahooId;
            entity.CbsPlayerId = player.CbsPlayerId;
            entity.PlayerByeWeek = player.PlayerByeWeek;
            entity.PlayerOwnedAverage = player.PlayerOwnedAverage;
            entity.PlayerOwnedEspn = player.PlayerOwnedEspn;
            entity.PlayerOwnedYahoo = player.PlayerOwnedYahoo;
            entity.PlayerEcrDelta = player.PlayerEcrDelta;
            entity.RankEcr = player.RankEcr;
            entity.RankMinimum = player.RankMinimum;
            entity.RankMaximum = player.RankMaximum;
            entity.RankAverage = player.RankAverage;
            entity.RankStandardDeviation = player.RankStandardDeviation;
            entity.PositionRank = player.PositionRank;
            entity.Tier = player.Tier;
            entity.Season = snapshot.Season;
            entity.Week = snapshot.Week;
            entity.RetrievedAtUtc = snapshot.RetrievedAtUtc;
            entity.RawJson = rawPlayerJson[index];
            entity.UpdatedAtUtc = updatedAtUtc;
        }
    }

    private static async Task ApplyFantasyProsEnrichmentAsync(LeagueApiDbContext dbContext, IReadOnlyList<FantasyProsRankingPlayer> fantasyProsPlayers, CancellationToken cancellationToken)
    {
        var fantasyProsPlayersByYahooId = fantasyProsPlayers
            .Where(player => int.TryParse(player.PlayerYahooId, out _))
            .GroupBy(player => int.Parse(player.PlayerYahooId!))
            .ToDictionary(group => group.Key, group => group.First());

        await dbContext.Players
            .Where(player => player.YahooId != null)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(player => player.PlayerOwnedAverage, (decimal?)null)
                    .SetProperty(player => player.RankAverage, (string?)null)
                    .SetProperty(player => player.PositionRank, (string?)null)
                    .SetProperty(player => player.Tier, (int?)null),
                cancellationToken);

        if (fantasyProsPlayersByYahooId.Count == 0)
        {
            return;
        }

        var yahooIds = fantasyProsPlayersByYahooId.Keys.ToArray();
        var matchedPlayers = await dbContext.Players
            .Where(player => player.YahooId != null && yahooIds.Contains(player.YahooId.Value))
            .ToListAsync(cancellationToken);

        foreach (var player in matchedPlayers)
        {
            var fantasyProsPlayer = fantasyProsPlayersByYahooId[player.YahooId!.Value];
            player.PlayerOwnedAverage = fantasyProsPlayer.PlayerOwnedAverage;
            player.RankAverage = fantasyProsPlayer.RankAverage;
            player.PositionRank = fantasyProsPlayer.PositionRank;
            player.Tier = fantasyProsPlayer.Tier;
        }
    }

    private async Task MarkFailedAsync(Guid syncRunId, string errorMessage)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(CancellationToken.None);
        var syncRun = await dbContext.FantasyProsSyncRuns.SingleAsync(run => run.SyncRunId == syncRunId, CancellationToken.None);
        syncRun.CompletedAtUtc = DateTimeOffset.UtcNow;
        syncRun.Status = FailedStatus;
        syncRun.ErrorMessage = errorMessage;
        await dbContext.SaveChangesAsync(CancellationToken.None);
        _logger.LogError("FantasyPros snapshot import {SyncRunId} failed: {ErrorMessage}", syncRunId, errorMessage);
    }
}
