using System.Text.Json.Serialization;

namespace LeagueAPI.Models;

public sealed class ScoringTemplate
{
    public required string TemplateKey { get; set; }

    public required string Name { get; set; }

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public List<ScoringTemplateRule> Rules { get; set; } = [];

    [JsonIgnore]
    public List<WeeklyPlayerPoint> WeeklyPlayerPoints { get; set; } = [];
}
