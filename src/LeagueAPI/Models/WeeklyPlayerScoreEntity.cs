namespace LeagueAPI.Models;

public sealed class WeeklyPlayerScoreEntity
{
    public int Season { get; set; }

    public int Week { get; set; }

    public int FantasyProsPlayerId { get; set; }

    public string? SleeperPlayerId { get; set; }

    public string? PlayerName { get; set; }

    public string? PositionId { get; set; }

    public string? TeamId { get; set; }

    public decimal Points { get; set; }

    public Guid? SyncRunId { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
