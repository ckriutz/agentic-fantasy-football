using System.Text.Json.Serialization;

namespace LeagueAPI.Models;

public sealed class WeeklyPlayerPoint
{
    public long WeeklyPlayerPointId { get; set; }

    public long WeeklyPlayerStatId { get; set; }

    public required string TemplateKey { get; set; }

    public decimal FantasyPoints { get; set; }

    public string? BreakdownJson { get; set; }

    public DateTimeOffset CalculatedAtUtc { get; set; }

    [JsonIgnore]
    public WeeklyPlayerStat WeeklyPlayerStat { get; set; } = null!;

    [JsonIgnore]
    public ScoringTemplate ScoringTemplate { get; set; } = null!;
}
