using LeagueAPI.Models;

namespace LeagueAPI.Services;

public interface IDecisionWriter
{
    Task<DecisionEntity> LogDecisionAsync(
        string agentId,
        int week,
        string type,
        string reasoning,
        string action,
        CancellationToken cancellationToken);
}
