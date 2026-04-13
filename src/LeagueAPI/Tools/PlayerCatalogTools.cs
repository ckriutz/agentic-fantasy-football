using ModelContextProtocol.Server;
using LeagueAPI.Models;
using LeagueAPI.Services;
using System.ComponentModel;

namespace LeagueAPI.Tools;

[McpServerToolType]
public sealed class PlayerCatalogTools(
    IPlayerCatalogReader playerCatalogReader,
    JsonFileSyncStateStore syncStateStore)
{
    private readonly IPlayerCatalogReader _playerCatalogReader = playerCatalogReader;
    private readonly JsonFileSyncStateStore _syncStateStore = syncStateStore;

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

    [McpServerTool, Description("Search players by name, team, or position.")]
    public Task<IReadOnlyList<PlayerRecord>> SearchPlayers(
        [Description("Optional player name search text.")] string? name = null,
        [Description("Optional team abbreviation or team code.")] string? team = null,
        [Description("Optional position such as QB, RB, WR, or TE.")] string? position = null,
        [Description("Maximum number of players to return.")] int limit = 25)
    {
        return _playerCatalogReader.QueryAsync(
            new PlayerQuery
            {
                Name = name,
                Team = team,
                Position = position,
                Limit = limit
            },
            CancellationToken.None);
    }

    [McpServerTool, Description("Get the latest Sleeper player sync status.")]
    public Task<SleeperSyncState> GetLatestSleeperSyncStatus()
    {
        return _syncStateStore.GetLatestStateAsync(CancellationToken.None);
    }
}
