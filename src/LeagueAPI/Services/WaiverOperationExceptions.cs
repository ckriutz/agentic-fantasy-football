namespace LeagueAPI.Services;

public sealed class LeaguePhaseException(string requiredPhase, string currentPhase, int season, int week, string message)
    : InvalidOperationException(message)
{
    public string RequiredPhase { get; } = requiredPhase;

    public string CurrentPhase { get; } = currentPhase;

    public int Season { get; } = season;

    public int Week { get; } = week;
}

public enum WaiverClaimFailureType
{
    EmptyClaims,
    DuplicateClaimOrder,
    DuplicateAddPlayer,
    MissingAddPlayer,
    AddDropSamePlayer,
    AddPlayerNotFound,
    AddPlayerIneligible,
    DropPlayerNotOnRoster,
    RosterFull
}

public sealed class WaiverClaimValidationException(WaiverClaimFailureType failureType, string message)
    : ArgumentException(message, "claims")
{
    public WaiverClaimFailureType FailureType { get; } = failureType;

    public string? AgentId { get; init; }

    public string? AddSleeperPlayerId { get; init; }

    public string? DropSleeperPlayerId { get; init; }

    public string? PlayerPosition { get; init; }

    public int? CurrentRosterSize { get; init; }

    public int? MaxRosterSize { get; init; }

    public int? ClaimOrder { get; init; }
}

public enum FreeAgentFailureType
{
    AddPlayerNotFound,
    AddPlayerIneligible,
    AddPlayerLocked,
    AddPlayerAlreadyOwned,
    DropPlayerNotOnRoster,
    DropPlayerLocked,
    RosterFull,
    ConcurrencyConflict
}

public sealed class FreeAgentOperationException(FreeAgentFailureType failureType, string message, Exception? innerException = null)
    : InvalidOperationException(message, innerException)
{
    public FreeAgentFailureType FailureType { get; } = failureType;

    public string? AgentId { get; init; }

    public string? AddSleeperPlayerId { get; init; }

    public string? DropSleeperPlayerId { get; init; }

    public string? OwnerAgentId { get; init; }

    public string? PlayerPosition { get; init; }

    public int? CurrentRosterSize { get; init; }

    public int? MaxRosterSize { get; init; }

    public string? LockReason { get; init; }
}
