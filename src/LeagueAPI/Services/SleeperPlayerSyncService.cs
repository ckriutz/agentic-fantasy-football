using System.Text.Json;
using LeagueAPI.Models;
using Microsoft.Extensions.Logging;

namespace LeagueAPI.Services;

public sealed class SleeperPlayerSyncService(
    SleeperApiClient sleeperApiClient,
    FileSleeperSnapshotStore sleeperSnapshotStore,
    JsonFileSyncStateStore syncStateStore,
    IEnumerable<IPlayerCatalogPersistence> playerCatalogPersistenceServices,
    ILogger<SleeperPlayerSyncService> logger)
{
    private readonly SleeperApiClient _sleeperApiClient = sleeperApiClient;
    private readonly FileSleeperSnapshotStore _sleeperSnapshotStore = sleeperSnapshotStore;
    private readonly JsonFileSyncStateStore _syncStateStore = syncStateStore;
    private readonly IPlayerCatalogPersistence? _playerCatalogPersistence = playerCatalogPersistenceServices.FirstOrDefault();
    private readonly ILogger<SleeperPlayerSyncService> _logger = logger;
    private readonly SemaphoreSlim _syncLock = new(1, 1);

    public async Task<SleeperSyncExecutionResult> SyncPlayersAsync(bool force, CancellationToken cancellationToken)
    {
        await _syncLock.WaitAsync(cancellationToken);

        try
        {
            var currentState = await _syncStateStore.GetLatestStateAsync(cancellationToken);
            var nowUtc = DateTimeOffset.UtcNow;

            if (!force && currentState.LastSuccessfulSyncAtUtc?.UtcDateTime.Date == nowUtc.UtcDateTime.Date)
            {
                return new SleeperSyncExecutionResult("Skipped", currentState);
            }

            var syncRunId = Guid.NewGuid();
            if (_playerCatalogPersistence is not null)
                await _playerCatalogPersistence.RecordSyncStartedAsync(syncRunId, nowUtc, cancellationToken);

            try
            {
                var payload = await _sleeperApiClient.GetPlayersJsonAsync(cancellationToken);
                var playersResponse =
                    JsonSerializer.Deserialize<SleeperPlayersResponse>(payload)
                    ?? throw new InvalidOperationException("Sleeper returned an invalid players response.");

                var snapshot = await _sleeperSnapshotStore.SavePlayersSnapshotAsync(
                    payload,
                    nowUtc,
                    cancellationToken);

                var players = playersResponse
                    .Where(pair => !PlayerRecordFactory.ShouldIgnore(pair.Value))
                    .Select(pair => PlayerRecordFactory.Create(pair.Key, pair.Value))
                    .ToArray();

                var ignoredPlayerCount = playersResponse.Count - players.Length;
                if (ignoredPlayerCount > 0)
                {
                    _logger.LogInformation(
                        "Ignored {IgnoredPlayerCount} Sleeper placeholder players during sync run {SyncRunId}.",
                        ignoredPlayerCount,
                        syncRunId);
                }

                if (_playerCatalogPersistence is not null)
                    await _playerCatalogPersistence.PersistPlayersAsync(
                        players,
                        syncRunId,
                        nowUtc,
                        cancellationToken);

                var successState = new SleeperSyncState
                {
                    SyncRunId = syncRunId,
                    Status = "Succeeded",
                    LastAttemptedAtUtc = nowUtc,
                    LastSuccessfulSyncAtUtc = nowUtc,
                    RecordCount = players.Length,
                    SnapshotFileName = snapshot.FileName,
                    SnapshotRelativePath = snapshot.RelativePath,
                    PayloadSha256 = snapshot.PayloadSha256
                };

                await _syncStateStore.SaveStateAsync(successState, cancellationToken);
                if (_playerCatalogPersistence is not null)
                    await _playerCatalogPersistence.RecordSyncCompletedAsync(successState, cancellationToken);

                return new SleeperSyncExecutionResult("Succeeded", successState);
            }
            catch (Exception exception)
            {
                var failedState = new SleeperSyncState
                {
                    SyncRunId = syncRunId,
                    Status = "Failed",
                    LastAttemptedAtUtc = nowUtc,
                    LastSuccessfulSyncAtUtc = currentState.LastSuccessfulSyncAtUtc,
                    RecordCount = currentState.RecordCount,
                    SnapshotFileName = currentState.SnapshotFileName,
                    SnapshotRelativePath = currentState.SnapshotRelativePath,
                    PayloadSha256 = currentState.PayloadSha256,
                    ErrorMessage = exception.Message
                };

                await _syncStateStore.SaveStateAsync(failedState, cancellationToken);
                if (_playerCatalogPersistence is not null)
                    await _playerCatalogPersistence.RecordSyncFailedAsync(
                        syncRunId,
                        nowUtc,
                        exception.Message,
                        cancellationToken);

                throw;
            }
        }
        finally
        {
            _syncLock.Release();
        }
    }
}
