namespace LeagueAPI.Models;

public sealed record RosterPlayerResult(
    PlayerRecord Player,
    string? OwnerAgentId,
    bool IsAvailable,
    DateTimeOffset? AcquiredAtUtc,
    string? AcquisitionSource);
