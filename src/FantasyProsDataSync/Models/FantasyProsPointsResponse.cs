using System.Text.Json.Serialization;

namespace FantasyProsDataSync.Models;

public sealed class FantasyProsPointsResponse
{
    [JsonPropertyName("season")]
    public string? Season { get; init; }

    [JsonPropertyName("scoring")]
    public string? Scoring { get; init; }

    [JsonPropertyName("tier")]
    public string? Tier { get; init; }

    [JsonPropertyName("public_api_limited")]
    public bool PublicApiLimited { get; init; }

    [JsonPropertyName("players")]
    public IReadOnlyList<FantasyProsPlayerPoints> Players { get; init; } = [];
}
