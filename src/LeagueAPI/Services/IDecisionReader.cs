using LeagueAPI.Models;

namespace LeagueAPI.Services;

public interface IDecisionReader
{
    Task<IReadOnlyList<DecisionEntity>> GetDecisionsByAgentAsync(
        string agentId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<DecisionEntity>> GetAllDecisionsAsync(
        string? agentId,
        string? type,
        int? week,
        int limit,
        CancellationToken cancellationToken);
}
