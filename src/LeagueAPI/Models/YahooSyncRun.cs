using System.Text.Json.Serialization;

namespace LeagueAPI.Models;

public sealed class YahooSyncRun
{
    public Guid SyncRunId { get; set; }

    public required string GameKey { get; set; }

    public int Season { get; set; }

    public int? Week { get; set; }

    public DateTimeOffset StartedAtUtc { get; set; }

    public DateTimeOffset? CompletedAtUtc { get; set; }

    public required string Status { get; set; }

    public int? PageCount { get; set; }

    public int? RecordCount { get; set; }

    public int? MatchedPlayerCount { get; set; }

    public int? UnmatchedPlayerCount { get; set; }

    public string? ErrorMessage { get; set; }

    [JsonIgnore]
    public List<WeeklyPlayerStat> WeeklyPlayerStats { get; set; } = [];
}
