using LeagueAPI.Models;

namespace LeagueAPI.Services;

public interface IWaiverService
{
    Task SeedWaiverPriorityAsync(IReadOnlyList<string> draftOrder, bool force, CancellationToken cancellationToken);

    Task<IReadOnlyList<WaiverClaimResult>> SubmitWaiverClaimsAsync(string agentId, int season, int week, IReadOnlyList<WaiverClaimItem> claims, CancellationToken cancellationToken);

    Task<WaiverClaimResult> SubmitWaiverClaimForCurrentWeekAsync(string agentId, string addSleeperPlayerId, string? dropSleeperPlayerId, CancellationToken cancellationToken);

    Task<ProcessWaiverClaimsResult> ProcessWaiverClaimsAsync(int season, int week, CancellationToken cancellationToken);

    Task<AddFreeAgentResult> AddFreeAgentAsync(string agentId, int season, int week, string addSleeperPlayerId, string? dropSleeperPlayerId, CancellationToken cancellationToken);

    Task<AddFreeAgentResult> AddFreeAgentForCurrentWeekAsync(string agentId, string addSleeperPlayerId, string? dropSleeperPlayerId, CancellationToken cancellationToken);

    Task<IReadOnlyList<WaiverClaimResult>> GetWaiverClaimsAsync(int season, int week, string? agentId, CancellationToken cancellationToken);

    Task<WaiverPriorityResult> GetWaiverPriorityAsync(CancellationToken cancellationToken);

    Task<WaiverProcessStatusResult> GetWaiverProcessStatusAsync(int season, int week, CancellationToken cancellationToken);

    Task<MyWaiverStatusResult> GetMyWaiverStatusAsync(string agentId, int season, int week, CancellationToken cancellationToken);
}
