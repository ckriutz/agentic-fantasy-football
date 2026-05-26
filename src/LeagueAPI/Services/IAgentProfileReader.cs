using LeagueAPI.Models;

namespace LeagueAPI.Services;

public interface IAgentProfileReader
{
    Task<IReadOnlyList<AgentProfile>> GetAgentProfilesAsync(bool enabledOnly, CancellationToken cancellationToken);

    Task<AgentProfile?> GetAgentProfileAsync(string agentId, CancellationToken cancellationToken);
}
