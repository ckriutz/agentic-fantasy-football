using System.ComponentModel;
using LeagueAPI.Models;
using LeagueAPI.Services;
using ModelContextProtocol.Server;

namespace LeagueAPI.Tools;

[McpServerToolType]
public sealed class RosterMoveTools(RosterMoveService rosterMoveService)
{
    private readonly RosterMoveService _rosterMoveService = rosterMoveService;

    [McpServerTool(UseStructuredContent = true), Description("Make one phase-aware roster move. During drafting, the player is added immediately. During the waiver window, the move becomes a pending waiver claim. During free agency, the add/drop is completed immediately. Roster moves are rejected in locked or complete phases. Check the ok field and the result status: 'completed' means the roster changed, while 'pending_waiver' means the claim was submitted but the roster has not changed yet.")]
    public async Task<ToolResult<RosterMoveResult, WaiverOperationErrorDetails>> MakeRosterMove([Description("Your exact agent ID, such as player-01.")] string agentId, [Description("Sleeper player ID to add. Required for drafting, waivers, and free-agent acquisitions. Omit only for a pure drop during free agency.")] string? addSleeperPlayerId = null, [Description("Optional Sleeper player ID to drop. Use when a full roster needs space, or omit addSleeperPlayerId to make a pure drop during free agency.")] string? dropSleeperPlayerId = null)
    {
        try
        {
            var result = await _rosterMoveService.MakeRosterMoveAsync(agentId, addSleeperPlayerId, dropSleeperPlayerId, acquisitionSource: null, CancellationToken.None);
            return ToolResult<RosterMoveResult, WaiverOperationErrorDetails>.Success(result);
        }
        catch (LeaguePhaseException exception)
        {
            return Failure(
                "invalid_league_phase",
                exception.Message,
                agentId,
                addSleeperPlayerId,
                dropSleeperPlayerId,
                "Wait until drafting, the waiver window, or free agency before making a roster move.",
                requiredPhase: exception.RequiredPhase,
                currentPhase: exception.CurrentPhase,
                season: exception.Season,
                week: exception.Week);
        }
        catch (WaiverClaimValidationException exception)
        {
            return Failure(
                "invalid_waiver_move",
                exception.Message,
                exception.AgentId ?? agentId,
                exception.AddSleeperPlayerId ?? addSleeperPlayerId,
                exception.DropSleeperPlayerId ?? dropSleeperPlayerId,
                "Refresh your roster and available players, correct the identified issue once, then retry.",
                currentRosterSize: exception.CurrentRosterSize,
                maxRosterSize: exception.MaxRosterSize);
        }
        catch (FreeAgentOperationException exception)
        {
            return Failure(
                GetFreeAgentErrorCode(exception.FailureType),
                exception.Message,
                exception.AgentId ?? agentId,
                exception.AddSleeperPlayerId ?? addSleeperPlayerId,
                exception.DropSleeperPlayerId ?? dropSleeperPlayerId,
                "Refresh your roster and player availability, then make one corrected move or stop.",
                ownerAgentId: exception.OwnerAgentId,
                currentRosterSize: exception.CurrentRosterSize,
                maxRosterSize: exception.MaxRosterSize,
                lockReason: exception.LockReason);
        }
        catch (RosterPlayerOwnershipConflictException exception)
        {
            if (addSleeperPlayerId is null)
            {
                return Failure(
                    "player_owned_by_other_agent",
                    exception.Message,
                    exception.RequestedAgentId,
                    null,
                    exception.SleeperPlayerId,
                    "Call GetMyRoster and choose a player owned by your agent.",
                    ownerAgentId: exception.OwnerAgentId);
            }

            var alreadyOnRequestedRoster = string.Equals(exception.RequestedAgentId, exception.OwnerAgentId, StringComparison.Ordinal);
            return Failure(
                alreadyOnRequestedRoster ? "player_already_on_roster" : exception.OwnerAgentId is null ? "player_no_longer_available" : "player_owned_by_other_agent",
                exception.Message,
                exception.RequestedAgentId,
                exception.SleeperPlayerId,
                dropSleeperPlayerId,
                alreadyOnRequestedRoster
                    ? "Do not add the player again. Call GetMyRoster to inspect the current roster."
                    : "Refresh player availability and choose an available player.",
                ownerAgentId: exception.OwnerAgentId);
        }
        catch (RosterFullException exception)
        {
            return Failure(
                "roster_full",
                exception.Message,
                exception.AgentId,
                addSleeperPlayerId,
                dropSleeperPlayerId,
                "Provide a valid dropSleeperPlayerId from your roster, then retry.",
                currentRosterSize: exception.CurrentRosterSize,
                maxRosterSize: exception.MaxRosterSize);
        }
        catch (RosterPlayerIneligibleException exception)
        {
            return Failure("player_ineligible", exception.Message, agentId, exception.SleeperPlayerId, dropSleeperPlayerId, "Choose an active player eligible for this league's roster slots.");
        }
        catch (RosterPlayerNotFoundException exception)
        {
            return Failure(
                addSleeperPlayerId is null ? "player_not_on_roster" : "player_not_found",
                exception.Message,
                agentId,
                addSleeperPlayerId,
                dropSleeperPlayerId,
                addSleeperPlayerId is null
                    ? "Call GetMyRoster and choose a player currently on your roster."
                    : "Refresh your roster and available players, then use valid Sleeper player IDs.");
        }
        catch (ArgumentException exception)
        {
            return Failure("invalid_request", exception.Message, agentId, addSleeperPlayerId, dropSleeperPlayerId, "Correct the roster move arguments, then retry once.");
        }
    }

    private static ToolResult<RosterMoveResult, WaiverOperationErrorDetails> Failure(string code, string message, string? agentId, string? addSleeperPlayerId, string? dropSleeperPlayerId, string nextStep, string? ownerAgentId = null, int? currentRosterSize = null, int? maxRosterSize = null, string? requiredPhase = null, string? currentPhase = null, int? season = null, int? week = null, string? lockReason = null)
    {
        return ToolResult<RosterMoveResult, WaiverOperationErrorDetails>.Failure(
            code,
            message,
            new WaiverOperationErrorDetails
            {
                AgentId = agentId,
                AddSleeperPlayerId = addSleeperPlayerId,
                DropSleeperPlayerId = dropSleeperPlayerId,
                OwnerAgentId = ownerAgentId,
                CurrentRosterSize = currentRosterSize,
                MaxRosterSize = maxRosterSize,
                RequiredPhase = requiredPhase,
                CurrentPhase = currentPhase,
                Season = season,
                Week = week,
                LockReason = lockReason
            },
            nextStep);
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
}
