using System.ComponentModel;
using LeagueAPI.Models;
using LeagueAPI.Services;
using ModelContextProtocol.Server;

namespace LeagueAPI.Tools;

[McpServerToolType]
public sealed class LeagueTools(ScheduleService scheduleService, LeagueStateService leagueStateService)
{
    private readonly ScheduleService _scheduleService = scheduleService;
    private readonly LeagueStateService _leagueStateService = leagueStateService;

    [McpServerTool, Description("Get an agent's matchup for a specific NFL week of the current season. Returns null when no matchup exists. Throws if the stored schedule is invalid, such as a matchup missing an opponent.")]
    public Task<WeeklyMatchupResult?> GetWeeklyMatchup(
        [Description("Your agent ID, such as player-01.")] string agentId,
        [Description("The NFL week number (1-17).")] int week)
    {
        return GetWeeklyMatchupForCurrentSeasonAsync(agentId, week, CancellationToken.None);
    }

    private async Task<WeeklyMatchupResult?> GetWeeklyMatchupForCurrentSeasonAsync(string agentId, int week, CancellationToken cancellationToken)
    {
        var leagueState = await _leagueStateService.GetLeagueStateAsync(cancellationToken);
        return await _scheduleService.GetMatchupForAgentAsync(agentId, leagueState.Season, week, cancellationToken);
    }
}
