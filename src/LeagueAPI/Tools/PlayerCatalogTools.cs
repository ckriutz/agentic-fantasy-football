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

    [McpServerTool, Description("Get an NFL player by Sleeper player ID.")]
    public Task<PlayerRecord?> GetPlayerBySleeperId(
        [Description("The Sleeper player ID.")] string sleeperPlayerId)
    {
        return _playerCatalogReader.GetBySleeperIdAsync(sleeperPlayerId, CancellationToken.None);
    }

    //[McpServerTool, Description("Get an NFL player by Yahoo player ID.")]
    //public Task<PlayerRecord?> GetPlayerByYahooId(
    //    [Description("The Yahoo player ID.")] int yahooId)
    //{
    //    return _playerCatalogReader.GetByYahooIdAsync(yahooId, CancellationToken.None);
    //}

    // [McpServerTool, Description("Search players by name, team, position, or bye week. Results default to Sleeper search rank unless sortBy is provided.")]
    // public Task<IReadOnlyList<PlayerRecord>> SearchPlayers(
    //     [Description("Optional player name search text.")] string? name = null,
    //     [Description("Optional team abbreviation or team code.")] string? team = null,
    //     [Description("Optional position such as QB, RB, WR, or TE.")] string? position = null,
    //     [Description("Optional bye week filter.")] int? byeWeek = null,
    //     [Description("Optional sort field: name, projectedPoints, adp, lastSeasonPoints, or auctionValue.")] string? sortBy = null,
    //     [Description("When true, sorts descending.")] bool sortDescending = false,
    //     [Description("Maximum number of players to return.")] int limit = 25)
    // {
    //     return _playerCatalogReader.QueryAsync(
    //         new PlayerQuery
    //         {
    //             Name = name,
    //             Team = team,
    //             Position = position,
    //             ByeWeek = byeWeek,
    //             SortBy = sortBy,
    //             SortDescending = sortDescending,
    //             Limit = limit
    //         },
    //         CancellationToken.None);
    // }

    [McpServerTool, Description("Search active players by name, team, position, or bye week and include ownership and availability metadata.")]
    public Task<IReadOnlyList<RosterPlayerResult>> SearchPlayers(
        [Description("Optional player name search text.")] string? name = null,
        [Description("Optional team abbreviation or team code.")] string? team = null,
        [Description("Optional position such as QB, RB, WR, or TE.")] string? position = null,
        [Description("Optional bye week filter.")] int? byeWeek = null,
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
                SortBy = sortBy,
                SortDescending = sortDescending,
                Limit = limit
            },
            CancellationToken.None);
    }

    [McpServerTool, Description("Search active players that are not currently on any roster, ranked by Sleeper search rank where lower values are better.")]
    public Task<IReadOnlyList<RosterPlayerResult>> GetAvailablePlayers(
        [Description("Optional player name search text.")] string? name = null,
        [Description("Optional team abbreviation or team code.")] string? team = null,
        [Description("Optional position such as QB, RB, WR, or TE.")] string? position = null,
        [Description("Optional bye week filter.")] int? byeWeek = null,
        [Description("Maximum number of players to return.")] int limit = 25)
    {
        return _rosterReader.GetAvailablePlayersAsync(
            new PlayerQuery
            {
                Name = name,
                Team = team,
                Position = position,
                ByeWeek = byeWeek,
                Limit = limit
            },
            CancellationToken.None);
    }

    [McpServerTool, Description("Get ownership and availability for a single player.")]
    public Task<RosterPlayerResult?> GetPlayerAvailability([Description("The Sleeper player ID.")] string sleeperPlayerId)
    {
        return _rosterReader.GetPlayerAvailabilityAsync(sleeperPlayerId, CancellationToken.None);
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
