namespace LeagueAPI.Services;

public enum RosterMoveFailureType
{
    InvalidSlotType,
    PlayerNotOnRoster,
    PlayerOwnedByOtherAgent,
    IneligibleSlot,
    LineupLocked
}

public sealed class RosterMoveValidationException(RosterMoveFailureType failureType, string message, string sleeperPlayerId, string requestedSlotType) : InvalidOperationException(message)
{
    public RosterMoveFailureType FailureType { get; } = failureType;

    public string SleeperPlayerId { get; } = sleeperPlayerId;

    public string RequestedSlotType { get; } = requestedSlotType;

    public string? CurrentSlotType { get; init; }

    public string? OwnerAgentId { get; init; }

    public string? PlayerPosition { get; init; }

    public IReadOnlyList<string>? ValidSlotTypes { get; init; }

    public IReadOnlyList<string>? EligibleSlotTypes { get; init; }

    public string? LockReason { get; init; }
}
