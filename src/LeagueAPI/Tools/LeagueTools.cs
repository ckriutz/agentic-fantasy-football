using System.ComponentModel;
using LeagueAPI.Models;
using LeagueAPI.Services;
using ModelContextProtocol.Server;

namespace LeagueAPI.Tools;

[McpServerToolType]
public sealed class LeagueTools(IScheduleService scheduleService)
{
    private readonly IScheduleService _scheduleService = scheduleService;

    [McpServerTool, Description("Get an agent's matchup for a specific NFL week. Returns null when no matchup exists. Throws if the stored schedule is invalid, such as a matchup missing an opponent.")]
    public Task<WeeklyMatchupResult?> GetWeeklyMatchup(
        [Description("Your agent ID, such as player-01.")] string agentId,
        [Description("The NFL week number (1-17).")] int week)
    {
        return _scheduleService.GetMatchupForAgentAsync(agentId, week, CancellationToken.None);
    }
}
