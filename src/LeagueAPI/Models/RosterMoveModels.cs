namespace LeagueAPI.Models;

public sealed record MakeRosterMoveRequest(string AgentId, string? AddSleeperPlayerId, string? DropSleeperPlayerId, string? AcquisitionSource);

public sealed record RosterMoveResult(
    string Status,
    string Phase,
    int Season,
    int Week,
    string AgentId,
    string? AddedSleeperPlayerId,
    string? DroppedSleeperPlayerId,
    Guid? WaiverClaimId,
    string Message);

public static class RosterMoveStatuses
{
    public const string Completed = "completed";
    public const string PendingWaiver = "pending_waiver";
}
