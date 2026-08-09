using ModelContextProtocol.Server;
using LeagueAPI.Models;
using LeagueAPI.Services;
using System.ComponentModel;

namespace LeagueAPI.Tools;

[McpServerToolType]
public sealed class PlayerCatalogTools(
    IPlayerCatalogReader playerCatalogReader,
    IPlayerCatalogPersistence playerCatalogPersistence,
    IRosterReader rosterReader,
    SportsDataPlayerSyncService sportsDataPlayerSyncService)
{
    private readonly IPlayerCatalogReader _playerCatalogReader = playerCatalogReader;
    private readonly IPlayerCatalogPersistence _playerCatalogPersistence = playerCatalogPersistence;
    private readonly IRosterReader _rosterReader = rosterReader;
    private readonly SportsDataPlayerSyncService _sportsDataPlayerSyncService = sportsDataPlayerSyncService;

    // Returns the raw catalog record (no Active filter), unlike GetPlayerAvailability which is roster/availability aware.
    [McpServerTool, Description("Get an NFL player by Sleeper player ID.")]
    public Task<PlayerRecord?> GetPlayerBySleeperId(
        [Description("The Sleeper player ID.")] string sleeperPlayerId)
    {
        return _playerCatalogReader.GetBySleeperIdAsync(sleeperPlayerId, CancellationToken.None);
    }

    [McpServerTool, Description("Search active players by name, team, position, or bye week and include ownership, availability, and current lock status metadata.")]
    public async Task<IReadOnlyList<RosterToolPlayerResult>> SearchPlayers(
        [Description("Optional player name search text.")] string? name = null,
        [Description("Optional team abbreviation or team code.")] string? team = null,
        [Description("Optional position such as QB, RB, WR, or TE.")] string? position = null,
        [Description("Optional bye week filter.")] int? byeWeek = null,
        [Description("Optional sort field: name, projectedPoints, adp, lastSeasonPoints, or auctionValue.")] string? sortBy = null,
        [Description("When true, sorts descending.")] bool sortDescending = false,
        [Description("Maximum number of players to return.")] int limit = 25)
    {
        var players = await _rosterReader.QueryPlayersAsync(
            new PlayerQuery
            {
                Name = name,
                Team = team,
                Position = position,
                ByeWeek = byeWeek,
                SortBy = sortBy,
                SortDescending = sortDescending,
                Limit = limit
            },
            CancellationToken.None);

        return players.Select(RosterToolPlayerResult.FromRosterPlayerResult).ToList();
    }

    [McpServerTool, Description("Search active players that are not currently on any roster, ordered by projected fantasy points where higher is better, and include current add/drop and lineup lock status metadata. Projected points are season totals and are only comparable within a position, so pass the position argument to get a useful candidate list.")]
    public async Task<IReadOnlyList<RosterToolPlayerResult>> GetAvailablePlayers(
        [Description("Optional player name search text.")] string? name = null,
        [Description("Optional team abbreviation or team code.")] string? team = null,
        [Description("Optional position such as QB, RB, WR, or TE.")] string? position = null,
        [Description("Optional bye week filter.")] int? byeWeek = null,
        [Description("Maximum number of players to return.")] int limit = 25)
    {
        var players = await _rosterReader.GetAvailablePlayersAsync(
            new PlayerQuery
            {
                Name = name,
                Team = team,
                Position = position,
                ByeWeek = byeWeek,
                Limit = limit
            },
            CancellationToken.None);

        return players.Select(RosterToolPlayerResult.FromRosterPlayerResult).ToList();
    }

    [McpServerTool, Description("Get ownership, availability, and current lock status for a single player.")]
    public async Task<RosterToolPlayerResult?> GetPlayerAvailability([Description("The Sleeper player ID.")] string sleeperPlayerId)
    {
        var player = await _rosterReader.GetPlayerAvailabilityAsync(sleeperPlayerId, CancellationToken.None);
        return player is null ? null : RosterToolPlayerResult.FromRosterPlayerResult(player);
    }

    [McpServerTool, Description("Get the latest Sleeper player sync status.")]
    public Task<SleeperSyncState> GetLatestSleeperSyncStatus()
    {
        return _playerCatalogPersistence.GetLatestSyncStateAsync(CancellationToken.None);
    }

    [McpServerTool, Description("Get the latest SportsData player sync status.")]
    public Task<SportsDataSyncRun?> GetLatestSportsDataSyncStatus()
    {
        return _sportsDataPlayerSyncService.GetLatestSyncRunAsync(CancellationToken.None);
    }
}
