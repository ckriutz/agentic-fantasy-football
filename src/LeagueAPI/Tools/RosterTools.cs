using ModelContextProtocol.Server;
using LeagueAPI.Models;
using LeagueAPI.Services;
using System.ComponentModel;

namespace LeagueAPI.Tools;

[McpServerToolType]
public sealed class RosterTools(IRosterReader rosterReader, IRosterWriter rosterWriter)
{
    private readonly IRosterReader _rosterReader = rosterReader;
    private readonly IRosterWriter _rosterWriter = rosterWriter;

    [McpServerTool, Description("Get the current roster for an agent.")]
    public Task<IReadOnlyList<RosterPlayerResult>> GetMyRoster([Description("The agent ID, such as player-01.")] string agentId)
    {
        return _rosterReader.GetRosterAsync(agentId, CancellationToken.None);
    }

    [McpServerTool, Description("Add a player to an agent roster. Fails if another agent already owns the player.")]
    public Task<RosterPlayerResult> AddPlayerToRoster(
        [Description("The agent ID, such as player-01.")] string agentId,
        [Description("The Sleeper player ID.")] string sleeperPlayerId,
        [Description("How the player was acquired, such as manual, draft, waiver, or trade.")] string acquisitionSource = "manual")
    {
        return _rosterWriter.AddPlayerToRosterAsync(
            agentId,
            sleeperPlayerId,
            acquisitionSource,
            CancellationToken.None);
    }

    [McpServerTool, Description("Remove a player from an agent roster.")]
    public Task<RosterPlayerResult> RemovePlayerFromRoster(
        [Description("The agent ID, such as player-01.")] string agentId,
        [Description("The Sleeper player ID.")] string sleeperPlayerId)
    {
        return _rosterWriter.RemovePlayerFromRosterAsync(
            agentId,
            sleeperPlayerId,
            CancellationToken.None);
    }
}
