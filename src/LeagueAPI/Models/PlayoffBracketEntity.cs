namespace LeagueAPI.Models;

public static class PlayoffBracketStatuses
{
    public const string Projected = "projected";
    public const string Locked = "locked";
    public const string Complete = "complete";
}

public sealed class PlayoffBracketEntity
{
    public int Id { get; set; }

    public int Season { get; set; }

    public string Status { get; set; } = PlayoffBracketStatuses.Projected;

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
