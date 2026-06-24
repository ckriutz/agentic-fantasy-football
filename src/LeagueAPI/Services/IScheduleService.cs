using LeagueAPI.Models;

namespace LeagueAPI.Services;

public interface IScheduleService
{
    Task<GenerateScheduleResult> GenerateScheduleAsync(bool force, CancellationToken cancellationToken);

    Task<IReadOnlyList<ScheduleMatchupResult>> GetScheduleAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<ScheduleMatchupResult>> GetScheduleForWeekAsync(int week, CancellationToken cancellationToken);

    Task<WeeklyMatchupResult?> GetMatchupForAgentAsync(string agentId, int week, CancellationToken cancellationToken);
}
