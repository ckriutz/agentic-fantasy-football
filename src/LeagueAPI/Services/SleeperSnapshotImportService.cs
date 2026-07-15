using System.Security.Cryptography;
using System.Text.Json;
using Azure;
using Azure.Storage.Blobs;
using LeagueAPI.Data;
using LeagueAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace LeagueAPI.Services;

public sealed class SleeperSnapshotImportService(BlobServiceClient blobServiceClient, IPlayerCatalogPersistence playerCatalogPersistence, IDbContextFactory<LeagueApiDbContext> dbContextFactory, ILogger<SleeperSnapshotImportService> logger)
{
    private const string StartedStatus = "Started";
    private const string SucceededStatus = "Succeeded";
    private const string FailedStatus = "Failed";
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly BlobServiceClient _blobServiceClient = blobServiceClient;
    private readonly IPlayerCatalogPersistence _playerCatalogPersistence = playerCatalogPersistence;
    private readonly IDbContextFactory<LeagueApiDbContext> _dbContextFactory = dbContextFactory;
    private readonly ILogger<SleeperSnapshotImportService> _logger = logger;
    private readonly SemaphoreSlim _syncLock = new(1, 1);

    public async Task<SleeperSyncRun> ImportAsync(SleeperSnapshotImportRequest request, CancellationToken cancellationToken)
    {
        request = ValidateRequest(request);
        await _syncLock.WaitAsync(cancellationToken);

        try
        {
            var syncRun = new SleeperSyncRun
            {
                SyncRunId = Guid.NewGuid(),
                ContainerName = request.ContainerName,
                BlobName = request.BlobName,
                RetrievedAtUtc = request.RetrievedAtUtc,
                StartedAtUtc = DateTimeOffset.UtcNow,
                Status = StartedStatus
            };

            await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            dbContext.SleeperSyncRuns.Add(syncRun);
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
                var snapshot = document.RootElement.Deserialize<SleeperPlayersSnapshot>(SerializerOptions) ?? throw new InvalidDataException("The Sleeper blob does not contain a valid snapshot.");
                ValidateSnapshot(snapshot, request, document.RootElement);

                var alreadyProcessed = await dbContext.SleeperSyncRuns.AsNoTracking()
                    .AnyAsync(run =>
                        run.SyncRunId != syncRun.SyncRunId
                        && run.Status == SucceededStatus
                        && run.ContainerName == request.ContainerName
                        && run.BlobName == request.BlobName
                        && run.RetrievedAtUtc == request.RetrievedAtUtc
                        && run.ContentHash == contentHash,
                        cancellationToken);

                var recordCount = 0;
                if (!alreadyProcessed)
                {
                    var players = document.RootElement.GetProperty("players").EnumerateObject()
                        .Select(property =>
                        {
                            var player = property.Value.Deserialize<SleeperPlayer>(SerializerOptions)
                                ?? throw new InvalidDataException($"Sleeper player '{property.Name}' could not be deserialized.");
                            return PlayerRecordFactory.Create(property.Name, player);
                        })
                        .Where(player => !PlayerRecordFactory.ShouldIgnore(player))
                        .ToArray();

                    recordCount = players.Length;
                    await _playerCatalogPersistence.PersistPlayersAsync(players, syncRun.SyncRunId, DateTimeOffset.UtcNow, cancellationToken);
                }
                else
                {
                    recordCount = document.RootElement.GetProperty("players").EnumerateObject().Count();
                }

                syncRun.CompletedAtUtc = DateTimeOffset.UtcNow;
                syncRun.Status = SucceededStatus;
                syncRun.RecordCount = recordCount;
                syncRun.AlreadyProcessed = alreadyProcessed;
                syncRun.ErrorMessage = null;
                await dbContext.SaveChangesAsync(cancellationToken);

                _logger.LogInformation(
                    "Sleeper snapshot import {SyncRunId} succeeded for {ContainerName}/{BlobName}: {RecordCount} players, already processed: {AlreadyProcessed}.",
                    syncRun.SyncRunId,
                    request.ContainerName,
                    request.BlobName,
                    recordCount,
                    alreadyProcessed);

                return syncRun;
            }
            catch (OperationCanceledException)
            {
                await MarkFailedAsync(syncRun.SyncRunId, "The Sleeper snapshot import was canceled.");
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

    private static SleeperSnapshotImportRequest ValidateRequest(SleeperSnapshotImportRequest request)
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

    private static void ValidateSnapshot(SleeperPlayersSnapshot snapshot, SleeperSnapshotImportRequest request, JsonElement root)
    {
        if (snapshot.RetrievedAtUtc != request.RetrievedAtUtc)
        {
            throw new InvalidDataException($"Snapshot retrieval time {snapshot.RetrievedAtUtc:O} does not match requested retrieval time {request.RetrievedAtUtc:O}.");
        }

        if (snapshot.RetrievedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new InvalidDataException("Snapshot RetrievedAtUtc must use the UTC offset.");
        }

        if (!root.TryGetProperty("players", out var playersElement) || playersElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("The Sleeper snapshot must contain a players object.");
        }

        if (playersElement.EnumerateObject().Any() is false)
        {
            throw new InvalidDataException("The Sleeper snapshot must contain at least one player.");
        }
    }

    private async Task MarkFailedAsync(Guid syncRunId, string errorMessage)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(CancellationToken.None);
        var syncRun = await dbContext.SleeperSyncRuns.SingleAsync(run => run.SyncRunId == syncRunId, CancellationToken.None);
        syncRun.CompletedAtUtc = DateTimeOffset.UtcNow;
        syncRun.Status = FailedStatus;
        syncRun.ErrorMessage = errorMessage;
        await dbContext.SaveChangesAsync(CancellationToken.None);
        _logger.LogError("Sleeper snapshot import {SyncRunId} failed: {ErrorMessage}", syncRunId, errorMessage);
    }
}
