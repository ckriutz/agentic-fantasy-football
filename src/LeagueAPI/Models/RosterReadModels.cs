namespace LeagueAPI.Models;

public sealed record RosterPlayerResult(
    PlayerRecord Player,
    string? OwnerAgentId,
    bool IsAvailable,
    DateTimeOffset? AcquiredAtUtc,
    string? AcquisitionSource,
    string? SlotType,
    bool IsStarter,
    IReadOnlyDictionary<int, decimal> WeeklyPoints,
    PlayerLockStatus LockStatus)
{
    public static IReadOnlyDictionary<int, decimal> EmptyWeeklyPoints { get; } = new Dictionary<int, decimal>();
}
