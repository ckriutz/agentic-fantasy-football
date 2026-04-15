using System.Text.Json.Serialization;

namespace LeagueAPI.Models;

public sealed class ScoringTemplateRule
{
    public required string TemplateKey { get; set; }

    public int StatId { get; set; }

    public string? StatName { get; set; }

    public decimal Modifier { get; set; }

    [JsonIgnore]
    public ScoringTemplate ScoringTemplate { get; set; } = null!;
}
