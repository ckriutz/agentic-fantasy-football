namespace LeagueAPI.Models;

public sealed class RosterMoveErrorDetails
{
    public string? SleeperPlayerId { get; init; }

    public string? RequestedSlotType { get; init; }

    public string? CurrentSlotType { get; init; }

    public string? OwnerAgentId { get; init; }

    public string? PlayerPosition { get; init; }

    public IReadOnlyList<string>? ValidSlotTypes { get; init; }

    public IReadOnlyList<string>? EligibleSlotTypes { get; init; }

    public string? LockReason { get; init; }
}
