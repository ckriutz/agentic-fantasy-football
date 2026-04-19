using LeagueAPI.Models;

namespace LeagueAPI.Services;

public interface IRosterReader
{
    Task<IReadOnlyList<RosterPlayerResult>> GetRosterAsync(string agentId, CancellationToken cancellationToken);

    Task<IReadOnlyList<RosterPlayerResult>> QueryPlayersAsync(PlayerQuery query, CancellationToken cancellationToken);

    Task<IReadOnlyList<RosterPlayerResult>> GetAvailablePlayersAsync(PlayerQuery query, CancellationToken cancellationToken);

    Task<RosterPlayerResult?> GetPlayerAvailabilityAsync(string sleeperPlayerId, CancellationToken cancellationToken);
}
