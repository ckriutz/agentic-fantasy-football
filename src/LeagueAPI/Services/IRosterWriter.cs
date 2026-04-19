using LeagueAPI.Models;

namespace LeagueAPI.Services;

public interface IRosterWriter
{
    Task<RosterPlayerResult> AddPlayerToRosterAsync(
        string agentId,
        string sleeperPlayerId,
        string acquisitionSource,
        CancellationToken cancellationToken);

    Task<RosterPlayerResult> RemovePlayerFromRosterAsync(
        string agentId,
        string sleeperPlayerId,
        CancellationToken cancellationToken);
}
