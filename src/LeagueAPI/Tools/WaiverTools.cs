using ModelContextProtocol.Server;
using LeagueAPI.Models;
using LeagueAPI.Services;
using System.ComponentModel;

namespace LeagueAPI.Tools;

[McpServerToolType]
public sealed class WaiverTools(WaiverService waiverService, LeagueStateService leagueStateService)
{
    private readonly WaiverService _waiverService = waiverService;
    private readonly LeagueStateService _leagueStateService = leagueStateService;

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

    [McpServerTool(UseStructuredContent = true), Description("Submit a single waiver claim for the current league week. Call GetLeagueState first and only use this tool when the phase is 'waiver_window'. Provide dropSleeperPlayerId only if your roster is full and you need to drop someone to make room. Check the ok field: when true, read result; when false, read the error object's code, message, and nextStep, then take the action nextStep describes.")]
    public async Task<ToolResult<WaiverClaimResult, WaiverOperationErrorDetails>> SubmitWaiverClaimForCurrentWeek(
        [Description("Your agent ID, such as player-01.")] string agentId,
        [Description("Sleeper player ID of the player you want to add.")] string addSleeperPlayerId,
        [Description("Optional Sleeper player ID of the rostered player to drop if your roster is full.")] string? dropSleeperPlayerId = null)
    {
        try
        {
            var claim = await _waiverService.SubmitWaiverClaimForCurrentWeekAsync(agentId, addSleeperPlayerId, dropSleeperPlayerId, CancellationToken.None);
            return ToolResult<WaiverClaimResult, WaiverOperationErrorDetails>.Success(claim);
        }
        catch (LeaguePhaseException exception)
        {
            return ToolResult<WaiverClaimResult, WaiverOperationErrorDetails>.Failure(
                "invalid_league_phase",
                exception.Message,
                new WaiverOperationErrorDetails
                {
                    AgentId = agentId,
                    AddSleeperPlayerId = addSleeperPlayerId,
                    DropSleeperPlayerId = dropSleeperPlayerId,
                    RequiredPhase = exception.RequiredPhase,
                    CurrentPhase = exception.CurrentPhase,
                    Season = exception.Season,
                    Week = exception.Week
                },
                "Call GetLeagueState and only submit a waiver claim when the phase is 'waiver_window'.");
        }
        catch (WaiverClaimValidationException exception)
        {
            return ToolResult<WaiverClaimResult, WaiverOperationErrorDetails>.Failure(
                GetWaiverClaimErrorCode(exception.FailureType),
                exception.Message,
                new WaiverOperationErrorDetails
                {
                    AgentId = exception.AgentId ?? agentId,
                    AddSleeperPlayerId = exception.AddSleeperPlayerId ?? addSleeperPlayerId,
                    DropSleeperPlayerId = exception.DropSleeperPlayerId ?? dropSleeperPlayerId,
                    PlayerPosition = exception.PlayerPosition,
                    CurrentRosterSize = exception.CurrentRosterSize,
                    MaxRosterSize = exception.MaxRosterSize,
                    ClaimOrder = exception.ClaimOrder
                },
                GetWaiverClaimNextStep(exception.FailureType));
        }
        catch (ArgumentException exception)
        {
            return ToolResult<WaiverClaimResult, WaiverOperationErrorDetails>.Failure(
                "invalid_request",
                exception.Message,
                new WaiverOperationErrorDetails
                {
                    AgentId = agentId,
                    AddSleeperPlayerId = addSleeperPlayerId,
                    DropSleeperPlayerId = dropSleeperPlayerId
                },
                "Provide a valid agent ID and Sleeper player ID, then retry.");
        }
    }

    [McpServerTool(UseStructuredContent = true), Description("Add a free agent immediately for the current league week. Call GetLeagueState first and only use this tool when the phase is 'free_agency'. Provide dropSleeperPlayerId only if your roster is full and you need to drop someone to make room. Check the ok field: when true, read result; when false, read the error object's code, message, and nextStep, then take the action nextStep describes.")]
    public async Task<ToolResult<AddFreeAgentResult, WaiverOperationErrorDetails>> AddFreeAgentForCurrentWeek(
        [Description("Your agent ID, such as player-01.")] string agentId,
        [Description("Sleeper player ID of the free agent to add.")] string addSleeperPlayerId,
        [Description("Optional Sleeper player ID of the rostered player to drop if your roster is full.")] string? dropSleeperPlayerId = null)
    {
        try
        {
            var result = await _waiverService.AddFreeAgentForCurrentWeekAsync(agentId, addSleeperPlayerId, dropSleeperPlayerId, CancellationToken.None);
            return ToolResult<AddFreeAgentResult, WaiverOperationErrorDetails>.Success(result);
        }
        catch (LeaguePhaseException exception)
        {
            return ToolResult<AddFreeAgentResult, WaiverOperationErrorDetails>.Failure(
                "invalid_league_phase",
                exception.Message,
                new WaiverOperationErrorDetails
                {
                    AgentId = agentId,
                    AddSleeperPlayerId = addSleeperPlayerId,
                    DropSleeperPlayerId = dropSleeperPlayerId,
                    RequiredPhase = exception.RequiredPhase,
                    CurrentPhase = exception.CurrentPhase,
                    Season = exception.Season,
                    Week = exception.Week
                },
                "Call GetLeagueState and only add a free agent when the phase is 'free_agency'.");
        }
        catch (FreeAgentOperationException exception)
        {
            return ToolResult<AddFreeAgentResult, WaiverOperationErrorDetails>.Failure(
                GetFreeAgentErrorCode(exception.FailureType),
                exception.Message,
                new WaiverOperationErrorDetails
                {
                    AgentId = exception.AgentId ?? agentId,
                    AddSleeperPlayerId = exception.AddSleeperPlayerId ?? addSleeperPlayerId,
                    DropSleeperPlayerId = exception.DropSleeperPlayerId ?? dropSleeperPlayerId,
                    OwnerAgentId = exception.OwnerAgentId,
                    PlayerPosition = exception.PlayerPosition,
                    CurrentRosterSize = exception.CurrentRosterSize,
                    MaxRosterSize = exception.MaxRosterSize,
                    LockReason = exception.LockReason
                },
                GetFreeAgentNextStep(exception.FailureType));
        }
        catch (ArgumentException exception)
        {
            return ToolResult<AddFreeAgentResult, WaiverOperationErrorDetails>.Failure(
                "invalid_request",
                exception.Message,
                new WaiverOperationErrorDetails
                {
                    AgentId = agentId,
                    AddSleeperPlayerId = addSleeperPlayerId,
                    DropSleeperPlayerId = dropSleeperPlayerId
                },
                "Provide a valid agent ID and Sleeper player ID, then retry.");
        }
    }

    private static string GetWaiverClaimErrorCode(WaiverClaimFailureType failureType)
    {
        return failureType switch
        {
            WaiverClaimFailureType.EmptyClaims => "invalid_request",
            WaiverClaimFailureType.DuplicateClaimOrder => "invalid_request",
            WaiverClaimFailureType.DuplicateAddPlayer => "invalid_request",
            WaiverClaimFailureType.MissingAddPlayer => "invalid_request",
            WaiverClaimFailureType.AddDropSamePlayer => "invalid_request",
            WaiverClaimFailureType.AddPlayerNotFound => "player_not_found",
            WaiverClaimFailureType.AddPlayerIneligible => "player_ineligible",
            WaiverClaimFailureType.DropPlayerNotOnRoster => "invalid_drop_player",
            WaiverClaimFailureType.RosterFull => "roster_full",
            _ => throw new ArgumentOutOfRangeException(nameof(failureType), failureType, null)
        };
    }

    private static string GetWaiverClaimNextStep(WaiverClaimFailureType failureType)
    {
        return failureType switch
        {
            WaiverClaimFailureType.EmptyClaims => "Provide an addSleeperPlayerId, then retry.",
            WaiverClaimFailureType.DuplicateClaimOrder => "Provide a single claim with a valid addSleeperPlayerId, then retry.",
            WaiverClaimFailureType.DuplicateAddPlayer => "Provide a single claim with a distinct addSleeperPlayerId, then retry.",
            WaiverClaimFailureType.MissingAddPlayer => "Provide a valid addSleeperPlayerId, then retry.",
            WaiverClaimFailureType.AddDropSamePlayer => "Provide different add and drop players, then retry.",
            WaiverClaimFailureType.AddPlayerNotFound => "Call SearchPlayers or GetAvailablePlayers to find a valid active Sleeper player ID, then retry.",
            WaiverClaimFailureType.AddPlayerIneligible => "Choose an active player eligible for this league's roster slots.",
            WaiverClaimFailureType.DropPlayerNotOnRoster => "Call GetMyRoster and choose a dropSleeperPlayerId currently on your roster.",
            WaiverClaimFailureType.RosterFull => "Your roster is full. Provide a valid dropSleeperPlayerId from GetMyRoster, then retry.",
            _ => throw new ArgumentOutOfRangeException(nameof(failureType), failureType, null)
        };
    }

    private static string GetFreeAgentErrorCode(FreeAgentFailureType failureType)
    {
        return failureType switch
        {
            FreeAgentFailureType.AddPlayerNotFound => "player_not_found",
            FreeAgentFailureType.AddPlayerIneligible => "player_ineligible",
            FreeAgentFailureType.AddPlayerLocked => "player_locked",
            FreeAgentFailureType.AddPlayerAlreadyOwned => "player_owned_by_other_agent",
            FreeAgentFailureType.DropPlayerNotOnRoster => "invalid_drop_player",
            FreeAgentFailureType.DropPlayerLocked => "player_locked",
            FreeAgentFailureType.RosterFull => "roster_full",
            FreeAgentFailureType.ConcurrencyConflict => "player_no_longer_available",
            _ => throw new ArgumentOutOfRangeException(nameof(failureType), failureType, null)
        };
    }

    private static string GetFreeAgentNextStep(FreeAgentFailureType failureType)
    {
        return failureType switch
        {
            FreeAgentFailureType.AddPlayerNotFound => "Call SearchPlayers or GetAvailablePlayers to find a valid active Sleeper player ID, then retry.",
            FreeAgentFailureType.AddPlayerIneligible => "Choose an active player eligible for this league's roster slots.",
            FreeAgentFailureType.AddPlayerLocked => "The player's game has started or add/drop is locked. Choose an unlocked player.",
            FreeAgentFailureType.AddPlayerAlreadyOwned => "Call GetPlayerAvailability to confirm ownership, then choose an available free agent.",
            FreeAgentFailureType.DropPlayerNotOnRoster => "Call GetMyRoster and choose a dropSleeperPlayerId currently on your roster.",
            FreeAgentFailureType.DropPlayerLocked => "Choose a different rostered player to drop whose game has not started.",
            FreeAgentFailureType.RosterFull => "Your roster is full. Provide a valid dropSleeperPlayerId from GetMyRoster, then retry.",
            FreeAgentFailureType.ConcurrencyConflict => "The player was taken by another agent. Call GetPlayerAvailability and choose another free agent.",
            _ => throw new ArgumentOutOfRangeException(nameof(failureType), failureType, null)
        };
    }
}
