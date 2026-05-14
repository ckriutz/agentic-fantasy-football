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

    [McpServerTool, Description("Get the current roster for an player. Lists every player currently on the agent's roster.")]
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

    [McpServerTool, Description("Move a rostered player into a lineup slot. Valid starter slots are QB1, RB1, RB2, WR1, WR2, TE1, FLEX1, K1, DEF1. Use BN for bench.")]
    public Task<RosterPlayerResult> SetPlayerSlot(
        [Description("The agent ID, such as player-01.")] string agentId,
        [Description("The Sleeper player ID.")] string sleeperPlayerId,
        [Description("The slot type, such as QB1, RB1, FLEX1, K1, DEF1, or BN.")] string slotType)
    {
        return _rosterWriter.SetPlayerSlotAsync(
            agentId,
            sleeperPlayerId,
            slotType,
            CancellationToken.None);
    }

    [McpServerTool, Description("Automatically set the best valid starting lineup from the agent's current roster using Sleeper search rank. Unused players remain on BN.")]
    public Task<IReadOnlyList<RosterPlayerResult>> AutoSetLineup([Description("The agent ID, such as player-01.")] string agentId)
    {
        return _rosterWriter.AutoSetLineupAsync(agentId, CancellationToken.None);
    }
}
