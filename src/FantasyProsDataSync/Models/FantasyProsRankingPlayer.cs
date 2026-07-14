using System.Text.Json.Serialization;

namespace FantasyProsDataSync.Models;

public sealed class FantasyProsRankingPlayer
{
    [JsonPropertyName("player_id")]
    public int PlayerId { get; init; }

    [JsonPropertyName("player_name")]
    public string? PlayerName { get; init; }

    [JsonPropertyName("sportsdata_id")]
    public string? SportsDataId { get; init; }

    [JsonPropertyName("player_team_id")]
    public string? PlayerTeamId { get; init; }

    [JsonPropertyName("player_position_id")]
    public string? PlayerPositionId { get; init; }

    [JsonPropertyName("player_positions")]
    public string? PlayerPositions { get; init; }

    [JsonPropertyName("player_short_name")]
    public string? PlayerShortName { get; init; }

    [JsonPropertyName("player_eligibility")]
    public string? PlayerEligibility { get; init; }

    [JsonPropertyName("player_yahoo_positions")]
    public string? PlayerYahooPositions { get; init; }

    [JsonPropertyName("player_page_url")]
    public string? PlayerPageUrl { get; init; }

    [JsonPropertyName("player_filename")]
    public string? PlayerFilename { get; init; }

    [JsonPropertyName("player_yahoo_id")]
    public string? PlayerYahooId { get; init; }

    [JsonPropertyName("cbs_player_id")]
    public string? CbsPlayerId { get; init; }

    [JsonPropertyName("player_bye_week")]
    public string? PlayerByeWeek { get; init; }

    [JsonPropertyName("player_owned_avg")]
    public decimal? PlayerOwnedAverage { get; init; }

    [JsonPropertyName("player_owned_espn")]
    public decimal? PlayerOwnedEspn { get; init; }

    [JsonPropertyName("player_owned_yahoo")]
    public decimal? PlayerOwnedYahoo { get; init; }

    [JsonPropertyName("player_ecr_delta")]
    public decimal? PlayerEcrDelta { get; init; }

    [JsonPropertyName("rank_ecr")]
    public int RankEcr { get; init; }

    [JsonPropertyName("rank_min")]
    public string? RankMinimum { get; init; }

    [JsonPropertyName("rank_max")]
    public string? RankMaximum { get; init; }

    [JsonPropertyName("rank_ave")]
    public string? RankAverage { get; init; }

    [JsonPropertyName("rank_std")]
    public string? RankStandardDeviation { get; init; }

    [JsonPropertyName("pos_rank")]
    public string? PositionRank { get; init; }

    [JsonPropertyName("tier")]
    public int Tier { get; init; }
}
