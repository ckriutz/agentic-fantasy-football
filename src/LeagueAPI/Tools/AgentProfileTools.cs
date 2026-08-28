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

    [McpServerTool(UseStructuredContent = true), Description("Creates the agent's disabled profile when needed, then updates its team name in the database. Call this after choosing a team name during bootstrap. A newly created profile has blank model and connection values until configured separately. Check the ok field: when true, read result; when false, read the error object's code, message, and nextStep, then take the action nextStep describes.")]
    public async Task<ToolResult<AgentProfile, AgentProfileErrorDetails>> SetMyTeamName(
        [Description("The agent ID, such as player-01.")] string agentId,
        [Description("The team name chosen by the agent.")] string teamName)
    {
        try
        {
            var profile = await _agentProfileWriter.SetTeamNameAsync(agentId, teamName, CancellationToken.None);
            return ToolResult<AgentProfile, AgentProfileErrorDetails>.Success(profile);
        }
        catch (ArgumentException exception)
        {
            return ToolResult<AgentProfile, AgentProfileErrorDetails>.Failure(
                "invalid_request",
                exception.Message,
                new AgentProfileErrorDetails { AgentId = agentId, TeamName = teamName },
                "Provide a valid agent ID and a non-blank team name, then retry.");
        }
    }

    [McpServerTool(UseStructuredContent = true), Description("Creates the agent's disabled profile when needed, then updates its bootstrap status in the database. Set to true once the bootstrap file, team name, and logo have been created. A newly created profile has blank model and connection values until configured separately. Check the ok field: when true, read result; when false, read the error object's code, message, and nextStep, then take the action nextStep describes.")]
    public async Task<ToolResult<AgentProfile, AgentProfileErrorDetails>> SetMyBootstrapStatus(
        [Description("The agent ID, such as player-01.")] string agentId,
        [Description("True when bootstrap is complete.")] bool isBootstrapped)
    {
        try
        {
            var profile = await _agentProfileWriter.SetBootstrapStatusAsync(agentId, isBootstrapped, CancellationToken.None);
            return ToolResult<AgentProfile, AgentProfileErrorDetails>.Success(profile);
        }
        catch (ArgumentException exception)
        {
            return ToolResult<AgentProfile, AgentProfileErrorDetails>.Failure(
                "invalid_request",
                exception.Message,
                new AgentProfileErrorDetails { AgentId = agentId },
                "Provide a valid agent ID, then retry.");
        }
    }
}
