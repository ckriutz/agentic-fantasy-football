using System.Text.Json.Serialization;

namespace LeagueAPI.Models;

public sealed class WeeklyPlayerStatValue
{
    public long WeeklyPlayerStatId { get; set; }

    public int StatId { get; set; }

    public string? StatName { get; set; }

    public decimal Value { get; set; }

    [JsonIgnore]
    public WeeklyPlayerStat WeeklyPlayerStat { get; set; } = null!;
}
