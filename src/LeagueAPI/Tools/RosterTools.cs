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
    public Task<IReadOnlyList<RosterPlayerResult>> GetMyRoster(
        [Description("The agent ID, such as player-01.")] string agentId)
    {
        return _rosterReader.GetRosterAsync(agentId, CancellationToken.None);
    }

    [McpServerTool, Description("Search active players and include ownership and availability metadata.")]
    public Task<IReadOnlyList<RosterPlayerResult>> SearchPlayersWithOwnership(
        [Description("Optional player name search text.")] string? name = null,
        [Description("Optional team abbreviation or team code.")] string? team = null,
        [Description("Optional position such as QB, RB, WR, or TE.")] string? position = null,
        [Description("Optional bye week filter.")] int? byeWeek = null,
        [Description("Optional minimum projected fantasy points filter.")] decimal? minProjectedPoints = null,
        [Description("Optional maximum average draft position filter.")] decimal? maxAverageDraftPosition = null,
        [Description("Optional sort field: name, projectedPoints, adp, lastSeasonPoints, or auctionValue.")] string? sortBy = null,
        [Description("When true, sorts descending.")] bool sortDescending = false,
        [Description("Maximum number of players to return.")] int limit = 25)
    {
        return _rosterReader.QueryPlayersAsync(
            new PlayerQuery
            {
                Name = name,
                Team = team,
                Position = position,
                ByeWeek = byeWeek,
                MinProjectedPoints = minProjectedPoints,
                MaxAverageDraftPosition = maxAverageDraftPosition,
                SortBy = sortBy,
                SortDescending = sortDescending,
                Limit = limit
            },
            CancellationToken.None);
    }

    [McpServerTool, Description("Search active players that are not currently on any roster.")]
    public Task<IReadOnlyList<RosterPlayerResult>> GetAvailablePlayers(
        [Description("Optional player name search text.")] string? name = null,
        [Description("Optional team abbreviation or team code.")] string? team = null,
        [Description("Optional position such as QB, RB, WR, or TE.")] string? position = null,
        [Description("Optional bye week filter.")] int? byeWeek = null,
        [Description("Optional minimum projected fantasy points filter.")] decimal? minProjectedPoints = null,
        [Description("Optional maximum average draft position filter.")] decimal? maxAverageDraftPosition = null,
        [Description("Optional sort field: name, projectedPoints, adp, lastSeasonPoints, or auctionValue.")] string? sortBy = null,
        [Description("When true, sorts descending.")] bool sortDescending = false,
        [Description("Maximum number of players to return.")] int limit = 25)
    {
        return _rosterReader.GetAvailablePlayersAsync(
            new PlayerQuery
            {
                Name = name,
                Team = team,
                Position = position,
                ByeWeek = byeWeek,
                MinProjectedPoints = minProjectedPoints,
                MaxAverageDraftPosition = maxAverageDraftPosition,
                SortBy = sortBy,
                SortDescending = sortDescending,
                Limit = limit
            },
            CancellationToken.None);
    }

    [McpServerTool, Description("Get ownership and availability for a single player.")]
    public Task<RosterPlayerResult?> GetPlayerAvailability(
        [Description("The Sleeper player ID.")] string sleeperPlayerId)
    {
        return _rosterReader.GetPlayerAvailabilityAsync(sleeperPlayerId, CancellationToken.None);
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
