using LeagueAPI.Models;

namespace LeagueAPI.Services;

public interface IMatchupScoringService
{
    Task<MatchupScoreUpdateResult> UpdateLiveScoresAsync(int season, int week, CancellationToken cancellationToken);

    Task<MatchupScoreUpdateResult> FinalizeWeekAsync(int season, int week, CancellationToken cancellationToken);
}
