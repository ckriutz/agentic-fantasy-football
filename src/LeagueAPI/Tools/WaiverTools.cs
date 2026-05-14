using ModelContextProtocol.Server;
using LeagueAPI.Models;
using LeagueAPI.Services;
using System.ComponentModel;

namespace LeagueAPI.Tools;

[McpServerToolType]
public sealed class WaiverTools(IWaiverService waiverService)
{
    private readonly IWaiverService _waiverService = waiverService;

    [McpServerTool, Description("Get your complete waiver status for a given week in one call. Returns: the current phase (waiver_window or free_agency), your waiver priority position, whether you have pending claims, and all your claim details with results. Call this first before deciding whether to submit claims or add a free agent.")]
    public Task<MyWaiverStatusResult> GetMyWaiverStatus(
        [Description("Your agent ID, such as player-01.")] string agentId,
        [Description("The NFL season year, such as 2025.")] int season,
        [Description("The week number (1-17).")] int week)
    {
        return _waiverService.GetMyWaiverStatusAsync(agentId, season, week, CancellationToken.None);
    }

    [McpServerTool, Description("Submit a prioritized list of waiver claims. Each claim specifies a player to add and a player to drop. Replaces any existing pending claims for this agent and week. Claims are processed in ClaimOrder sequence — lower values are attempted first. Only one claim will succeed per waiver period.")]
    public Task<IReadOnlyList<WaiverClaimResult>> SubmitWaiverClaims(
        [Description("Your agent ID, such as player-01.")] string agentId,
        [Description("The NFL season year, such as 2025.")] int season,
        [Description("The week number (1-17).")] int week,
        [Description("Ordered list of waiver claims. Each claim needs a ClaimOrder (integer priority within your list), an AddSleeperPlayerId (the player you want), and a DropSleeperPlayerId (the player you will give up).")] IReadOnlyList<WaiverClaimItem> claims)
    {
        return _waiverService.SubmitWaiverClaimsAsync(agentId, season, week, claims, CancellationToken.None);
    }

    [McpServerTool, Description("Immediately add a free agent to your roster and drop a player in return. Only allowed when the phase is 'free_agency' (after waivers have been processed). Call GetMyWaiverStatus first to confirm. The added player is placed on the bench (BN).")]
    public Task<AddFreeAgentResult> AddFreeAgent(
        [Description("Your agent ID, such as player-01.")] string agentId,
        [Description("The NFL season year, such as 2025.")] int season,
        [Description("The week number (1-17).")] int week,
        [Description("Sleeper player ID of the free agent to add.")] string addSleeperPlayerId,
        [Description("Sleeper player ID of the rostered player to drop.")] string dropSleeperPlayerId)
    {
        return _waiverService.AddFreeAgentAsync(agentId, season, week, addSleeperPlayerId, dropSleeperPlayerId, CancellationToken.None);
    }
}
