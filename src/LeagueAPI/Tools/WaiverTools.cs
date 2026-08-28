using System.ComponentModel;
using LeagueAPI.Models;
using LeagueAPI.Services;
using ModelContextProtocol.Server;

namespace LeagueAPI.Tools;

[McpServerToolType]
public sealed class WaiverTools(WaiverService waiverService, LeagueStateService leagueStateService)
{
    private readonly WaiverService _waiverService = waiverService;
    private readonly LeagueStateService _leagueStateService = leagueStateService;

    [McpServerTool, Description("Get your complete waiver status for the current league week in one call. Uses the persisted league state to determine the current season and week. Returns the current league phase, your waiver priority position, whether you have pending claims, and enriched claim summaries including add/drop player details and outcomes.")]
    public async Task<MyWaiverStatusResult> GetMyWaiverStatus([Description("Your agent ID, such as player-01.")] string agentId)
    {
        var leagueState = await _leagueStateService.GetLeagueStateAsync(CancellationToken.None);
        return await _waiverService.GetMyWaiverStatusAsync(agentId, leagueState.Season, leagueState.Week, CancellationToken.None);
    }

    [McpServerTool, Description("Get your waiver status for a specific season and week. Keep this for admin or debugging scenarios when you need an explicit week instead of the persisted league state defaults.")]
    public Task<MyWaiverStatusResult> GetMyWaiverStatusForWeek([Description("Your agent ID, such as player-01.")] string agentId, [Description("The NFL season year, such as 2025.")] int season, [Description("The week number (1-17).")] int week)
    {
        return _waiverService.GetMyWaiverStatusAsync(agentId, season, week, CancellationToken.None);
    }
}
