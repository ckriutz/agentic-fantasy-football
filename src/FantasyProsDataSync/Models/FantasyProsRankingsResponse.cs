using System.Text.Json.Serialization;

namespace FantasyProsDataSync.Models;

public sealed class FantasyProsRankingsResponse
{
    [JsonPropertyName("sport")]
    public string? Sport { get; init; }

    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("ranking_type_name")]
    public string? RankingTypeName { get; init; }

    [JsonPropertyName("year")]
    public string? Year { get; init; }

    [JsonPropertyName("week")]
    public string? Week { get; init; }

    [JsonPropertyName("position_id")]
    public string? PositionId { get; init; }

    [JsonPropertyName("scoring")]
    public string? Scoring { get; init; }

    [JsonPropertyName("filters")]
    public string? Filters { get; init; }

    [JsonPropertyName("count")]
    public int Count { get; init; }

    [JsonPropertyName("total_experts")]
    public int TotalExperts { get; init; }

    [JsonPropertyName("last_updated")]
    public string? LastUpdated { get; init; }

    [JsonPropertyName("players")]
    public IReadOnlyList<FantasyProsRankingPlayer> Players { get; init; } = [];

    [JsonPropertyName("last_updated_ts")]
    public long LastUpdatedTimestamp { get; init; }

    [JsonPropertyName("public_api_limited")]
    public bool PublicApiLimited { get; init; }

    [JsonPropertyName("tier")]
    public string? Tier { get; init; }
}
