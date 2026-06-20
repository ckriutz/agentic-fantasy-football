using ModelContextProtocol.Server;
using LeagueAPI.Models;
using LeagueAPI.Services;
using System.ComponentModel;

namespace LeagueAPI.Tools;

[McpServerToolType]
public sealed class WaiverTools(IWaiverService waiverService, ILeagueStateService leagueStateService)
{
    private readonly IWaiverService _waiverService = waiverService;
    private readonly ILeagueStateService _leagueStateService = leagueStateService;

    [McpServerTool, Description("Get your complete waiver status for the current league week in one call. Uses the persisted league state to determine the current season and week. Returns the current league phase, your waiver priority position, whether you have pending claims, and enriched claim summaries including add/drop player details and outcomes.")]
    public async Task<MyWaiverStatusResult> GetMyWaiverStatus(
        [Description("Your agent ID, such as player-01.")] string agentId)
    {
        var leagueState = await _leagueStateService.GetLeagueStateAsync(CancellationToken.None);
        return await _waiverService.GetMyWaiverStatusAsync(agentId, leagueState.Season, leagueState.Week, CancellationToken.None);
    }

    [McpServerTool, Description("Get your waiver status for a specific season and week. Keep this for admin or debugging scenarios when you need an explicit week instead of the persisted league state defaults.")]
    public Task<MyWaiverStatusResult> GetMyWaiverStatusForWeek(
        [Description("Your agent ID, such as player-01.")] string agentId,
        [Description("The NFL season year, such as 2025.")] int season,
        [Description("The week number (1-17).")] int week)
    {
        return _waiverService.GetMyWaiverStatusAsync(agentId, season, week, CancellationToken.None);
    }

    [McpServerTool, Description("Submit a single waiver claim for the current league week. Call GetLeagueState first and only use this tool when the phase is 'waiver_window'. Provide DropSleeperPlayerId only if your roster is full and you need to drop someone to make room.")]
    public Task<WaiverClaimResult> SubmitWaiverClaimForCurrentWeek(
        [Description("Your agent ID, such as player-01.")] string agentId,
        [Description("Sleeper player ID of the player you want to add.")] string addSleeperPlayerId,
        [Description("Optional Sleeper player ID of the rostered player to drop if your roster is full.")] string? dropSleeperPlayerId = null)
    {
        return _waiverService.SubmitWaiverClaimForCurrentWeekAsync(agentId, addSleeperPlayerId, dropSleeperPlayerId, CancellationToken.None);
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

    [McpServerTool, Description("Add a free agent immediately for the current league week. Call GetLeagueState first and only use this tool when the phase is 'free_agency'. Provide DropSleeperPlayerId only if your roster is full and you need to drop someone to make room.")]
    public Task<AddFreeAgentResult> AddFreeAgentForCurrentWeek(
        [Description("Your agent ID, such as player-01.")] string agentId,
        [Description("Sleeper player ID of the free agent to add.")] string addSleeperPlayerId,
        [Description("Optional Sleeper player ID of the rostered player to drop if your roster is full.")] string? dropSleeperPlayerId = null)
    {
        return _waiverService.AddFreeAgentForCurrentWeekAsync(agentId, addSleeperPlayerId, dropSleeperPlayerId, CancellationToken.None);
    }

    [McpServerTool, Description("Immediately add a free agent to your roster for a specific season and week. Only allowed when the phase is 'free_agency'. Provide DropSleeperPlayerId if your roster is full and you need to make room. The added player is placed on the bench (BN).")]
    public Task<AddFreeAgentResult> AddFreeAgent(
        [Description("Your agent ID, such as player-01.")] string agentId,
        [Description("The NFL season year, such as 2025.")] int season,
        [Description("The week number (1-17).")] int week,
        [Description("Sleeper player ID of the free agent to add.")] string addSleeperPlayerId,
        [Description("Sleeper player ID of the rostered player to drop.")] string? dropSleeperPlayerId)
    {
        return _waiverService.AddFreeAgentAsync(agentId, season, week, addSleeperPlayerId, dropSleeperPlayerId, CancellationToken.None);
    }
}
