namespace LeagueAPI.Models;

public sealed record PlayerLockStatus(
    bool HasPlayedThisWeek,
    bool IsAddDropLocked,
    string? AddDropLockReason,
    bool IsLineupMoveLocked,
    string? LineupMoveLockReason)
{
    public static PlayerLockStatus Unlocked { get; } = new(
        false,
        false,
        null,
        false,
        null);
}
