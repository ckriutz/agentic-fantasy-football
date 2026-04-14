using ModelContextProtocol.Server;
using LeagueAPI.Models;
using LeagueAPI.Services;
using System.ComponentModel;

namespace LeagueAPI.Tools;

[McpServerToolType]
public sealed class PlayerCatalogTools(
    IPlayerCatalogReader playerCatalogReader,
    JsonFileSyncStateStore syncStateStore,
    SportsDataPlayerSyncService sportsDataPlayerSyncService)
{
    private readonly IPlayerCatalogReader _playerCatalogReader = playerCatalogReader;
    private readonly JsonFileSyncStateStore _syncStateStore = syncStateStore;
    private readonly SportsDataPlayerSyncService _sportsDataPlayerSyncService = sportsDataPlayerSyncService;

    [McpServerTool, Description("Get an NFL player by Sleeper player ID.")]
    public Task<PlayerRecord?> GetPlayerBySleeperId(
        [Description("The Sleeper player ID.")] string sleeperPlayerId)
    {
        return _playerCatalogReader.GetBySleeperIdAsync(sleeperPlayerId, CancellationToken.None);
    }

    [McpServerTool, Description("Get an NFL player by Yahoo player ID.")]
    public Task<PlayerRecord?> GetPlayerByYahooId(
        [Description("The Yahoo player ID.")] int yahooId)
    {
        return _playerCatalogReader.GetByYahooIdAsync(yahooId, CancellationToken.None);
    }

    [McpServerTool, Description("Search players by name, team, position, bye week, ADP, or projected points.")]
    public Task<IReadOnlyList<PlayerRecord>> SearchPlayers(
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
        return _playerCatalogReader.QueryAsync(
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

    [McpServerTool, Description("Get the latest Sleeper player sync status.")]
    public Task<SleeperSyncState> GetLatestSleeperSyncStatus()
    {
        return _syncStateStore.GetLatestStateAsync(CancellationToken.None);
    }

    [McpServerTool, Description("Get the latest SportsData player sync status.")]
    public Task<SportsDataSyncRun?> GetLatestSportsDataSyncStatus()
    {
        return _sportsDataPlayerSyncService.GetLatestSyncRunAsync(CancellationToken.None);
    }
}
