using System.ComponentModel;
using LeagueAPI.Models;
using LeagueAPI.Services;
using ModelContextProtocol.Server;

namespace LeagueAPI.Tools;

[McpServerToolType]
public sealed class LeagueStateTools(LeagueStateService leagueStateService)
{
    private readonly LeagueStateService _leagueStateService = leagueStateService;

    [McpServerTool, Description("Get the current persisted league state, including season, week, weekly phase such as drafting, waiver_window, free_agency, games_locked, or complete, and season stage such as draft, regular_season, playoffs, or complete.")]
    public Task<LeagueState> GetLeagueState()
    {
        return _leagueStateService.GetLeagueStateAsync(CancellationToken.None);
    }
}
