using PlayerDatabase.Models;

namespace PlayerDatabase.Services;

public interface IPlayerCatalogReader
{
    Task<PlayerRecord?> GetBySleeperIdAsync(string sleeperPlayerId, CancellationToken cancellationToken);

    Task<PlayerRecord?> GetByYahooIdAsync(int yahooId, CancellationToken cancellationToken);

    Task<IReadOnlyList<PlayerRecord>> QueryAsync(PlayerQuery query, CancellationToken cancellationToken);
}
