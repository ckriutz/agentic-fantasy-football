using LeagueAPI.Models;

namespace LeagueAPI.Services;

public interface ILeagueStateService
{
    Task<LeagueState> GetLeagueStateAsync(CancellationToken cancellationToken);

    Task<LeagueState> SetLeagueStateAsync(int season, int week, string phase, string updatedBy, CancellationToken cancellationToken);
}
