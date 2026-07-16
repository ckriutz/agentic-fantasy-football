using System.Security.Cryptography;
using System.Text.Json;
using Azure;
using Azure.Storage.Blobs;
using LeagueAPI.Data;
using LeagueAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace LeagueAPI.Services;

public sealed class SportsDataSnapshotImportService(BlobServiceClient blobServiceClient, IDbContextFactory<LeagueApiDbContext> dbContextFactory, ILogger<SportsDataSnapshotImportService> logger)
{
    private const string StartedStatus = "Started";
    private const string SucceededStatus = "Succeeded";
    private const string FailedStatus = "Failed";
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly BlobServiceClient _blobServiceClient = blobServiceClient;
    private readonly IDbContextFactory<LeagueApiDbContext> _dbContextFactory = dbContextFactory;
    private readonly ILogger<SportsDataSnapshotImportService> _logger = logger;
    private readonly SemaphoreSlim _syncLock = new(1, 1);

    public async Task<SportsDataSyncRun> ImportAsync(SportsDataSnapshotImportRequest request, CancellationToken cancellationToken)
    {
        request = ValidateRequest(request);
        await _syncLock.WaitAsync(cancellationToken);

        try
        {
            var syncRun = new SportsDataSyncRun
            {
                SyncRunId = Guid.NewGuid(),
                ContainerName = request.ContainerName,
                BlobName = request.BlobName,
                RetrievedAtUtc = request.RetrievedAtUtc,
                StartedAtUtc = DateTimeOffset.UtcNow,
                Status = StartedStatus
            };

            await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            dbContext.SportsDataSyncRuns.Add(syncRun);
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
                var snapshot = document.RootElement.Deserialize<SportsDataPlayersSnapshot>(SerializerOptions) ?? throw new InvalidDataException("The SportsData blob does not contain a valid snapshot.");
                ValidateSnapshot(snapshot, request);

                if (!document.RootElement.TryGetProperty("players", out var playerElements) || playerElements.ValueKind != JsonValueKind.Array)
                {
                    throw new InvalidDataException("The SportsData snapshot must contain a players array.");
                }

                var rawPlayerJson = playerElements.EnumerateArray().Select(player => player.GetRawText()).ToArray();
                if (rawPlayerJson.Length != snapshot.Players.Count)
                {
                    throw new InvalidDataException("The SportsData snapshot players could not be read consistently.");
                }

                var alreadyProcessed = await dbContext.SportsDataSyncRuns.AsNoTracking()
                    .AnyAsync(run =>
                        run.SyncRunId != syncRun.SyncRunId
                        && run.Status == SucceededStatus
                        && run.ContainerName == request.ContainerName
                        && run.BlobName == request.BlobName
                        && run.RetrievedAtUtc == request.RetrievedAtUtc
                        && run.ContentHash == contentHash,
                        cancellationToken);

                if (!alreadyProcessed)
                {
                    await UpsertSportsDataPlayersAsync(dbContext, snapshot.Players, rawPlayerJson, DateTimeOffset.UtcNow, cancellationToken);
                    await ApplySportsDataEnrichmentAsync(dbContext, snapshot.Players, cancellationToken);
                }

                syncRun.CompletedAtUtc = DateTimeOffset.UtcNow;
                syncRun.Status = SucceededStatus;
                syncRun.RecordCount = snapshot.Players.Count;
                syncRun.AlreadyProcessed = alreadyProcessed;
                syncRun.ErrorMessage = null;
                await dbContext.SaveChangesAsync(cancellationToken);

                _logger.LogInformation(
                    "SportsData snapshot import {SyncRunId} succeeded for {ContainerName}/{BlobName}: {RecordCount} players, already processed: {AlreadyProcessed}.",
                    syncRun.SyncRunId,
                    request.ContainerName,
                    request.BlobName,
                    snapshot.Players.Count,
                    alreadyProcessed);

                return syncRun;
            }
            catch (OperationCanceledException)
            {
                await MarkFailedAsync(syncRun.SyncRunId, "The SportsData snapshot import was canceled.");
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

    private static SportsDataSnapshotImportRequest ValidateRequest(SportsDataSnapshotImportRequest request)
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

        if (request.RetrievedAtUtc == default || request.RetrievedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("RetrievedAtUtc is required and must use the UTC offset.", nameof(request));
        }

        return request with { ContainerName = containerName, BlobName = blobName };
    }

    private static void ValidateSnapshot(SportsDataPlayersSnapshot snapshot, SportsDataSnapshotImportRequest request)
    {
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
            throw new InvalidDataException("The SportsData snapshot must contain at least one player.");
        }

        if (snapshot.Players.Any(player => player is null || player.PlayerID <= 0))
        {
            throw new InvalidDataException("Every SportsData player must have a positive PlayerID.");
        }

        var duplicatePlayerId = snapshot.Players.GroupBy(player => player.PlayerID).FirstOrDefault(group => group.Count() > 1)?.Key;
        if (duplicatePlayerId is not null)
        {
            throw new InvalidDataException($"The SportsData snapshot contains duplicate PlayerID {duplicatePlayerId}.");
        }
    }

    private static async Task UpsertSportsDataPlayersAsync(LeagueApiDbContext dbContext, IReadOnlyList<SportsDataFantasyPlayer> sportsDataPlayers, IReadOnlyList<string> rawPlayerJson, DateTimeOffset updatedAtUtc, CancellationToken cancellationToken)
    {
        var playerIds = sportsDataPlayers.Select(player => player.PlayerID).ToArray();
        var existingPlayersById = await dbContext.SportsDataFantasyPlayers
            .Where(entity => playerIds.Contains(entity.SportsDataPlayerId))
            .ToDictionaryAsync(entity => entity.SportsDataPlayerId, cancellationToken);

        for (var index = 0; index < sportsDataPlayers.Count; index++)
        {
            var sportsDataPlayer = sportsDataPlayers[index];
            if (!existingPlayersById.TryGetValue(sportsDataPlayer.PlayerID, out var entity))
            {
                entity = new SportsDataFantasyPlayerEntity
                {
                    SportsDataPlayerId = sportsDataPlayer.PlayerID,
                    RawJson = rawPlayerJson[index]
                };
                dbContext.SportsDataFantasyPlayers.Add(entity);
            }

            entity.Name = sportsDataPlayer.Name;
            entity.Team = sportsDataPlayer.Team;
            entity.Position = sportsDataPlayer.Position;
            entity.FantasyPlayerKey = sportsDataPlayer.FantasyPlayerKey;
            entity.AverageDraftPosition = sportsDataPlayer.AverageDraftPosition;
            entity.AverageDraftPositionPPR = sportsDataPlayer.AverageDraftPositionPPR;
            entity.ByeWeek = sportsDataPlayer.ByeWeek;
            entity.LastSeasonFantasyPoints = sportsDataPlayer.LastSeasonFantasyPoints;
            entity.ProjectedFantasyPoints = sportsDataPlayer.ProjectedFantasyPoints;
            entity.AuctionValue = sportsDataPlayer.AuctionValue;
            entity.AuctionValuePPR = sportsDataPlayer.AuctionValuePPR;
            entity.AverageDraftPositionIDP = sportsDataPlayer.AverageDraftPositionIDP;
            entity.AverageDraftPositionRookie = sportsDataPlayer.AverageDraftPositionRookie;
            entity.AverageDraftPositionDynasty = sportsDataPlayer.AverageDraftPositionDynasty;
            entity.AverageDraftPosition2QB = sportsDataPlayer.AverageDraftPosition2QB;
            entity.RawJson = rawPlayerJson[index];
            entity.UpdatedAtUtc = updatedAtUtc;
        }
    }

    private static async Task ApplySportsDataEnrichmentAsync(LeagueApiDbContext dbContext, IReadOnlyCollection<SportsDataFantasyPlayer> sportsDataPlayers, CancellationToken cancellationToken)
    {
        var sportsDataPlayersById = sportsDataPlayers.ToDictionary(player => player.PlayerID);
        var sleeperPlayers = await dbContext.Players
            .Where(player => player.FantasyDataId != null)
            .ToListAsync(cancellationToken);

        foreach (var sleeperPlayer in sleeperPlayers)
        {
            if (sleeperPlayer.FantasyDataId is int fantasyDataId
                && sportsDataPlayersById.TryGetValue(fantasyDataId, out var sportsDataPlayer))
            {
                sleeperPlayer.AverageDraftPosition = sportsDataPlayer.AverageDraftPosition;
                sleeperPlayer.ByeWeek = sportsDataPlayer.ByeWeek;
                sleeperPlayer.LastSeasonFantasyPoints = sportsDataPlayer.LastSeasonFantasyPoints;
                sleeperPlayer.ProjectedFantasyPoints = sportsDataPlayer.ProjectedFantasyPoints;
                sleeperPlayer.AuctionValue = sportsDataPlayer.AuctionValue;
            }
            else
            {
                sleeperPlayer.AverageDraftPosition = null;
                sleeperPlayer.ByeWeek = null;
                sleeperPlayer.LastSeasonFantasyPoints = null;
                sleeperPlayer.ProjectedFantasyPoints = null;
                sleeperPlayer.AuctionValue = null;
            }
        }
    }

    private async Task MarkFailedAsync(Guid syncRunId, string errorMessage)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(CancellationToken.None);
        var syncRun = await dbContext.SportsDataSyncRuns.SingleAsync(run => run.SyncRunId == syncRunId, CancellationToken.None);
        syncRun.CompletedAtUtc = DateTimeOffset.UtcNow;
        syncRun.Status = FailedStatus;
        syncRun.ErrorMessage = errorMessage;
        await dbContext.SaveChangesAsync(CancellationToken.None);
        _logger.LogError("SportsData snapshot import {SyncRunId} failed: {ErrorMessage}", syncRunId, errorMessage);
    }
}
