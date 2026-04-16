using System.Text.Json;
using LeagueAPI.Models;
using Microsoft.Extensions.Logging;

namespace LeagueAPI.Services;

public sealed class SleeperPlayerSyncService(
    SleeperApiClient sleeperApiClient,
    IPlayerCatalogPersistence playerCatalogPersistence,
    ILogger<SleeperPlayerSyncService> logger)
{
    private readonly SleeperApiClient _sleeperApiClient = sleeperApiClient;
    private readonly IPlayerCatalogPersistence _playerCatalogPersistence = playerCatalogPersistence;
    private readonly ILogger<SleeperPlayerSyncService> _logger = logger;
    private readonly SemaphoreSlim _syncLock = new(1, 1);

    public async Task<SleeperSyncExecutionResult> SyncPlayersAsync(bool force, CancellationToken cancellationToken)
    {
        await _syncLock.WaitAsync(cancellationToken);

        try
        {
            var currentState = await _playerCatalogPersistence.GetLatestSyncStateAsync(cancellationToken);
            var nowUtc = DateTimeOffset.UtcNow;

            if (!force && currentState.LastSuccessfulSyncAtUtc?.UtcDateTime.Date == nowUtc.UtcDateTime.Date)
            {
                return new SleeperSyncExecutionResult("Skipped", currentState);
            }

            var syncRunId = Guid.NewGuid();
            await _playerCatalogPersistence.RecordSyncStartedAsync(syncRunId, nowUtc, cancellationToken);

            try
            {
                var payload = await _sleeperApiClient.GetPlayersJsonAsync(cancellationToken);
                var playersResponse =
                    JsonSerializer.Deserialize<SleeperPlayersResponse>(payload)
                    ?? throw new InvalidOperationException("Sleeper returned an invalid players response.");

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
                    RecordCount = players.Length
                };

                await _playerCatalogPersistence.RecordSyncCompletedAsync(successState, cancellationToken);

                return new SleeperSyncExecutionResult("Succeeded", successState);
            }
            catch (Exception exception)
            {
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
