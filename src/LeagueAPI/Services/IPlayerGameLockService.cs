using LeagueAPI.Models;

namespace LeagueAPI.Services;

public interface IPlayerGameLockService
{
    Task<IReadOnlyDictionary<string, PlayerLockStatus>> GetPlayerLockStatusesAsync(
        IReadOnlyCollection<string> sleeperPlayerIds,
        CancellationToken cancellationToken);
}
