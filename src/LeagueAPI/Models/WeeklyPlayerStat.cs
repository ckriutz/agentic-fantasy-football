using System.Text.Json.Serialization;

namespace LeagueAPI.Models;

public sealed class WeeklyPlayerStat
{
    public long WeeklyPlayerStatId { get; set; }

    public required string GameKey { get; set; }

    public int Season { get; set; }

    public int Week { get; set; }

    public int YahooPlayerId { get; set; }

    public string? SleeperPlayerId { get; set; }

    public string? FullName { get; set; }

    public string? Team { get; set; }

    public string? Position { get; set; }

    public string? EditorialTeamAbbr { get; set; }

    public Guid? SyncRunId { get; set; }

    public required string RawJson { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    [JsonIgnore]
    public YahooSyncRun? SyncRun { get; set; }

    public List<WeeklyPlayerStatValue> StatValues { get; set; } = [];

    public List<WeeklyPlayerPoint> Points { get; set; } = [];
}
