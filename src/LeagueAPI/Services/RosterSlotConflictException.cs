namespace LeagueAPI.Services;

public sealed class RosterSlotConflictException(string message, string sleeperPlayerId, string requestedSlotType, Exception innerException)
    : RosterConflictException(message, innerException)
{
    public string SleeperPlayerId { get; } = sleeperPlayerId;

    public string RequestedSlotType { get; } = requestedSlotType;
}
