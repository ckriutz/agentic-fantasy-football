using LeagueAPI.Models;

namespace LeagueAPI.Services;

public interface IPlayerCatalogPersistence
{
    Task PersistPlayersAsync(
        IReadOnlyCollection<PlayerRecord> players,
        Guid syncRunId,
        DateTimeOffset persistedAtUtc,
        CancellationToken cancellationToken);

    Task<SleeperSyncState> GetLatestSyncStateAsync(CancellationToken cancellationToken);
}
