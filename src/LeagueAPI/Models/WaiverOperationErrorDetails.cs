namespace LeagueAPI.Models;

public sealed class WaiverOperationErrorDetails
{
    public string? AgentId { get; init; }

    public string? AddSleeperPlayerId { get; init; }

    public string? DropSleeperPlayerId { get; init; }

    public string? OwnerAgentId { get; init; }

    public string? PlayerPosition { get; init; }

    public int? CurrentRosterSize { get; init; }

    public int? MaxRosterSize { get; init; }

    public string? RequiredPhase { get; init; }

    public string? CurrentPhase { get; init; }

    public int? Season { get; init; }

    public int? Week { get; init; }

    public int? ClaimOrder { get; init; }

    public string? LockReason { get; init; }
}
