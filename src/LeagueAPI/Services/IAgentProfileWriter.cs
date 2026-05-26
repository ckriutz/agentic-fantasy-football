using LeagueAPI.Models;

namespace LeagueAPI.Services;

public interface IAgentProfileWriter
{
    Task<AgentProfile> UpsertAgentProfileAsync(string agentId, string modelName, string connection, string? teamName, bool? isBootstrapped, bool? isEnabled, CancellationToken cancellationToken);

    Task<AgentProfile?> SetTeamNameAsync(string agentId, string teamName, CancellationToken cancellationToken);

    Task<AgentProfile?> SetBootstrapStatusAsync(string agentId, bool isBootstrapped, CancellationToken cancellationToken);
}
