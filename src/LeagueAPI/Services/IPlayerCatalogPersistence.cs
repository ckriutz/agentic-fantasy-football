using LeagueAPI.Models;

namespace LeagueAPI.Services;

public interface IPlayerCatalogPersistence
{
    Task RecordSyncStartedAsync(Guid syncRunId, DateTimeOffset startedAtUtc, CancellationToken cancellationToken);

    Task PersistPlayersAsync(
        IReadOnlyCollection<PlayerRecord> players,
        Guid syncRunId,
        DateTimeOffset persistedAtUtc,
        CancellationToken cancellationToken);

    Task RecordSyncCompletedAsync(SleeperSyncState syncState, CancellationToken cancellationToken);

    Task RecordSyncFailedAsync(
        Guid syncRunId,
        DateTimeOffset failedAtUtc,
        string errorMessage,
        CancellationToken cancellationToken);

    Task<SleeperSyncState> GetLatestSyncStateAsync(CancellationToken cancellationToken);
}
