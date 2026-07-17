using ModelContextProtocol.Server;
using LeagueAPI.Models;
using LeagueAPI.Services;
using System.ComponentModel;

namespace LeagueAPI.Tools;

[McpServerToolType]
public sealed class RosterTools(IRosterReader rosterReader, IRosterWriter rosterWriter)
{
    private readonly IRosterReader _rosterReader = rosterReader;
    private readonly IRosterWriter _rosterWriter = rosterWriter;

    [McpServerTool, Description("Get the current roster for an player. Lists every player currently on the agent's roster and includes current add/drop and lineup lock status metadata.")]
    public async Task<IReadOnlyList<RosterToolPlayerResult>> GetMyRoster([Description("The agent ID, such as player-01.")] string agentId)
    {
        var roster = await _rosterReader.GetRosterAsync(agentId, CancellationToken.None);
        return roster.Select(RosterToolPlayerResult.FromRosterPlayerResult).ToList();
    }

    [McpServerTool, Description("Add a player to an agent roster. Fails if another agent already owns the player.")]
    public async Task<RosterToolPlayerResult> AddPlayerToRoster([Description("The agent ID, such as player-01.")] string agentId, [Description("The Sleeper player ID.")] string sleeperPlayerId, [Description("How the player was acquired, such as manual, draft, waiver, or trade.")] string acquisitionSource = "manual")
    {
        var player = await _rosterWriter.AddPlayerToRosterAsync(agentId, sleeperPlayerId, acquisitionSource, CancellationToken.None);

        return RosterToolPlayerResult.FromRosterPlayerResult(player);
    }

    [McpServerTool, Description("Remove a player from an agent roster.")]
    public async Task<RosterToolPlayerResult> RemovePlayerFromRoster([Description("The agent ID, such as player-01.")] string agentId, [Description("The Sleeper player ID.")] string sleeperPlayerId)
    {
        var player = await _rosterWriter.RemovePlayerFromRosterAsync(agentId, sleeperPlayerId, CancellationToken.None);

        return RosterToolPlayerResult.FromRosterPlayerResult(player);
    }

    [McpServerTool(UseStructuredContent = true), Description("Move a rostered player into a lineup slot. Valid starter slots are QB1, RB1, RB2, WR1, WR2, TE1, FLEX1, K1, DEF1. Use BN for bench. Check ok in the response; when false, use error details and follow error.nextStep.")]
    public async Task<ToolResult<RosterToolPlayerResult, RosterMoveErrorDetails>> SetPlayerSlot([Description("The agent ID, such as player-01.")] string agentId, [Description("The Sleeper player ID.")] string sleeperPlayerId, [Description("The slot type, such as QB1, RB1, FLEX1, K1, DEF1, or BN.")] string slotType)
    {
        try
        {
            var player = await _rosterWriter.SetPlayerSlotAsync(agentId, sleeperPlayerId, slotType, CancellationToken.None);
            return ToolResult<RosterToolPlayerResult, RosterMoveErrorDetails>.Success(RosterToolPlayerResult.FromRosterPlayerResult(player));
        }
        catch (RosterSlotConflictException exception)
        {
            return ToolResult<RosterToolPlayerResult, RosterMoveErrorDetails>.Failure(
                "slot_conflict",
                exception.Message,
                new RosterMoveErrorDetails
                {
                    SleeperPlayerId = exception.SleeperPlayerId,
                    RequestedSlotType = exception.RequestedSlotType
                },
                "Call GetMyRoster to refresh the lineup, then retry the move.");
        }
        catch (RosterMoveValidationException exception)
        {
            return ToolResult<RosterToolPlayerResult, RosterMoveErrorDetails>.Failure(
                GetRosterMoveErrorCode(exception.FailureType),
                exception.Message,
                CreateRosterMoveErrorDetails(exception),
                GetRosterMoveNextStep(exception.FailureType));
        }
        catch (ArgumentException exception)
        {
            return ToolResult<RosterToolPlayerResult, RosterMoveErrorDetails>.Failure(
                "invalid_request",
                exception.Message,
                new RosterMoveErrorDetails
                {
                    SleeperPlayerId = sleeperPlayerId,
                    RequestedSlotType = slotType
                },
                "Provide a valid agent ID, Sleeper player ID, and slot type, then retry.");
        }
    }

    [McpServerTool, Description("Automatically set the best valid starting lineup from the agent's current roster using Sleeper search rank. Unused players remain on BN.")]
    public async Task<IReadOnlyList<RosterToolPlayerResult>> AutoSetLineup([Description("The agent ID, such as player-01.")] string agentId)
    {
        var roster = await _rosterWriter.AutoSetLineupAsync(agentId, CancellationToken.None);
        return roster.Select(RosterToolPlayerResult.FromRosterPlayerResult).ToList();
    }

    private static RosterMoveErrorDetails CreateRosterMoveErrorDetails(RosterMoveValidationException exception)
    {
        return new RosterMoveErrorDetails
        {
            SleeperPlayerId = exception.SleeperPlayerId,
            RequestedSlotType = exception.RequestedSlotType,
            CurrentSlotType = exception.CurrentSlotType,
            OwnerAgentId = exception.OwnerAgentId,
            PlayerPosition = exception.PlayerPosition,
            ValidSlotTypes = exception.ValidSlotTypes,
            EligibleSlotTypes = exception.EligibleSlotTypes,
            LockReason = exception.LockReason
        };
    }

    private static string GetRosterMoveErrorCode(RosterMoveFailureType failureType)
    {
        return failureType switch
        {
            RosterMoveFailureType.InvalidSlotType => "invalid_slot_type",
            RosterMoveFailureType.PlayerNotOnRoster => "player_not_on_roster",
            RosterMoveFailureType.PlayerOwnedByOtherAgent => "player_owned_by_other_agent",
            RosterMoveFailureType.IneligibleSlot => "ineligible_slot",
            RosterMoveFailureType.LineupLocked => "lineup_locked",
            _ => throw new ArgumentOutOfRangeException(nameof(failureType), failureType, null)
        };
    }

    private static string GetRosterMoveNextStep(RosterMoveFailureType failureType)
    {
        return failureType switch
        {
            RosterMoveFailureType.InvalidSlotType => "Retry with one of details.validSlotTypes.",
            RosterMoveFailureType.PlayerNotOnRoster => "Call GetMyRoster to verify the Sleeper player ID and choose a player currently on your roster.",
            RosterMoveFailureType.PlayerOwnedByOtherAgent => "Call GetMyRoster and choose a player owned by your agent.",
            RosterMoveFailureType.IneligibleSlot => "Retry with one of details.eligibleSlotTypes.",
            RosterMoveFailureType.LineupLocked => "Choose another unlocked player or wait until the next league week.",
            _ => throw new ArgumentOutOfRangeException(nameof(failureType), failureType, null)
        };
    }
}
