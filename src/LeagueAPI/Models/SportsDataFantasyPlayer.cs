using System.Text.Json.Serialization;

namespace LeagueAPI.Models;

public sealed class SportsDataFantasyPlayer
{
    [JsonPropertyName("FantasyPlayerKey")]
    public string? FantasyPlayerKey { get; init; }

    [JsonPropertyName("PlayerID")]
    public int PlayerID { get; init; }

    [JsonPropertyName("Name")]
    public string? Name { get; init; }

    [JsonPropertyName("Team")]
    public string? Team { get; init; }

    [JsonPropertyName("Position")]
    public string? Position { get; init; }

    [JsonPropertyName("AverageDraftPosition")]
    public decimal? AverageDraftPosition { get; init; }

    [JsonPropertyName("AverageDraftPositionPPR")]
    public decimal? AverageDraftPositionPPR { get; init; }

    [JsonPropertyName("ByeWeek")]
    public int? ByeWeek { get; init; }

    [JsonPropertyName("LastSeasonFantasyPoints")]
    public decimal? LastSeasonFantasyPoints { get; init; }

    [JsonPropertyName("ProjectedFantasyPoints")]
    public decimal? ProjectedFantasyPoints { get; init; }

    [JsonPropertyName("AuctionValue")]
    public int? AuctionValue { get; init; }

    [JsonPropertyName("AuctionValuePPR")]
    public int? AuctionValuePPR { get; init; }

    [JsonPropertyName("AverageDraftPositionIDP")]
    public decimal? AverageDraftPositionIDP { get; init; }

    [JsonPropertyName("AverageDraftPositionRookie")]
    public decimal? AverageDraftPositionRookie { get; init; }

    [JsonPropertyName("AverageDraftPositionDynasty")]
    public decimal? AverageDraftPositionDynasty { get; init; }

    [JsonPropertyName("AverageDraftPosition2QB")]
    public decimal? AverageDraftPosition2QB { get; init; }
}
