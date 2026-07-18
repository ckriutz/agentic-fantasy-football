namespace LeagueAPI.Services;

public sealed class RosterPlayerOwnershipConflictException(string requestedAgentId, string sleeperPlayerId, string? ownerAgentId, string message, Exception? innerException = null) : RosterConflictException(message, innerException)
{
    public string RequestedAgentId { get; } = requestedAgentId;

    public string SleeperPlayerId { get; } = sleeperPlayerId;

    public string? OwnerAgentId { get; } = ownerAgentId;
}

public sealed class RosterFullException(string agentId, int currentRosterSize, int maxRosterSize, string message) : RosterConflictException(message)
{
    public string AgentId { get; } = agentId;

    public int CurrentRosterSize { get; } = currentRosterSize;

    public int MaxRosterSize { get; } = maxRosterSize;
}

public sealed class RosterPlayerIneligibleException(string sleeperPlayerId, string? playerPosition, string message) : ArgumentException(message, nameof(sleeperPlayerId))
{
    public string SleeperPlayerId { get; } = sleeperPlayerId;

    public string? PlayerPosition { get; } = playerPosition;
}
