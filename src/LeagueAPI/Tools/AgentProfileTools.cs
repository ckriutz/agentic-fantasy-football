using ModelContextProtocol.Server;
using LeagueAPI.Models;
using LeagueAPI.Services;
using System.ComponentModel;

namespace LeagueAPI.Tools;

[McpServerToolType]
public sealed class AgentProfileTools(IAgentProfileReader agentProfileReader, IAgentProfileWriter agentProfileWriter)
{
    private readonly IAgentProfileReader _agentProfileReader = agentProfileReader;
    private readonly IAgentProfileWriter _agentProfileWriter = agentProfileWriter;

    [McpServerTool, Description("Reads the agent's profile from the database. Returns the agent's current identity, model, team name, and bootstrap status.")]
    public async Task<AgentProfile?> GetMyProfile([Description("The agent ID, such as player-01.")] string agentId)
    {
        return await _agentProfileReader.GetAgentProfileAsync(agentId, CancellationToken.None);
    }

    [McpServerTool, Description("Updates the agent's team name in the database. Call this after choosing a team name during bootstrap.")]
    public async Task<AgentProfile?> SetMyTeamName(
        [Description("The agent ID, such as player-01.")] string agentId,
        [Description("The team name chosen by the agent.")] string teamName)
    {
        return await _agentProfileWriter.SetTeamNameAsync(agentId, teamName, CancellationToken.None);
    }

    [McpServerTool, Description("Updates the agent's bootstrap status in the database. Set to true once the bootstrap file, team name, and logo have been created.")]
    public async Task<AgentProfile?> SetMyBootstrapStatus(
        [Description("The agent ID, such as player-01.")] string agentId,
        [Description("True when bootstrap is complete.")] bool isBootstrapped)
    {
        return await _agentProfileWriter.SetBootstrapStatusAsync(agentId, isBootstrapped, CancellationToken.None);
    }
}
