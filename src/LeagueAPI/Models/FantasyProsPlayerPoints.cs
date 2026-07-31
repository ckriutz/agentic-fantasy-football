using System.Text.Json.Serialization;

namespace LeagueAPI.Models;

public sealed class FantasyProsPlayerPoints
{
    [JsonPropertyName("player_id")]
    public int PlayerId { get; init; }

    [JsonPropertyName("player_name")]
    public string? PlayerName { get; init; }

    [JsonPropertyName("position_id")]
    public string? PositionId { get; init; }

    [JsonPropertyName("team_id")]
    public string? TeamId { get; init; }

    [JsonPropertyName("filename")]
    public string? Filename { get; init; }

    [JsonPropertyName("games")]
    public int? Games { get; init; }

    [JsonPropertyName("points")]
    public decimal? Points { get; init; }

    [JsonPropertyName("average")]
    public decimal? Average { get; init; }

    [JsonPropertyName("weeks")]
    public Dictionary<string, decimal?>? Weeks { get; init; }
}
