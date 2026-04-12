using System.Text.Json.Serialization;

namespace PlayerDatabase.Models;

public sealed class SleeperPlayersResponse : Dictionary<string, SleeperPlayer>
{
}

public sealed class SleeperPlayer
{
    [JsonPropertyName("active")]
    public bool Active { get; init; }

    [JsonPropertyName("age")]
    public int? Age { get; init; }

    [JsonPropertyName("birth_city")]
    public string? BirthCity { get; init; }

    [JsonPropertyName("birth_country")]
    public string? BirthCountry { get; init; }

    [JsonPropertyName("birth_date")]
    public string? BirthDate { get; init; }

    [JsonPropertyName("birth_state")]
    public string? BirthState { get; init; }

    [JsonPropertyName("college")]
    public string? College { get; init; }

    [JsonPropertyName("competitions")]
    public List<string> Competitions { get; init; } = [];

    [JsonPropertyName("depth_chart_order")]
    public int? DepthChartOrder { get; init; }

    [JsonPropertyName("depth_chart_position")]
    public string? DepthChartPosition { get; init; }

    [JsonPropertyName("espn_id")]
    public int? EspnId { get; init; }

    [JsonPropertyName("fantasy_data_id")]
    public int? FantasyDataId { get; init; }

    [JsonPropertyName("fantasy_positions")]
    public List<string>? FantasyPositions { get; init; }

    [JsonPropertyName("first_name")]
    public string? FirstName { get; init; }

    [JsonPropertyName("full_name")]
    public string? FullName { get; init; }

    [JsonPropertyName("gsis_id")]
    public string? GsisId { get; init; }

    [JsonPropertyName("hashtag")]
    public string? Hashtag { get; init; }

    [JsonPropertyName("height")]
    public string? Height { get; init; }

    [JsonPropertyName("high_school")]
    public string? HighSchool { get; init; }

    [JsonPropertyName("injury_body_part")]
    public string? InjuryBodyPart { get; init; }

    [JsonPropertyName("injury_notes")]
    public string? InjuryNotes { get; init; }

    [JsonPropertyName("injury_start_date")]
    public string? InjuryStartDate { get; init; }

    [JsonPropertyName("injury_status")]
    public string? InjuryStatus { get; init; }

    [JsonPropertyName("kalshi_id")]
    public string? KalshiId { get; init; }

    [JsonPropertyName("last_name")]
    public string? LastName { get; init; }

    [JsonPropertyName("metadata")]
    public Dictionary<string, string>? Metadata { get; init; }

    [JsonPropertyName("news_updated")]
    public long? NewsUpdated { get; init; }

    [JsonPropertyName("number")]
    public int? Number { get; init; }

    [JsonPropertyName("oddsjam_id")]
    public string? OddsjamId { get; init; }

    [JsonPropertyName("opta_id")]
    public string? OptaId { get; init; }

    [JsonPropertyName("pandascore_id")]
    public string? PandascoreId { get; init; }

    [JsonPropertyName("player_id")]
    public string? PlayerId { get; init; }

    [JsonPropertyName("player_shard")]
    public string? PlayerShard { get; init; }

    [JsonPropertyName("position")]
    public string? Position { get; init; }

    [JsonPropertyName("practice_description")]
    public string? PracticeDescription { get; init; }

    [JsonPropertyName("practice_participation")]
    public string? PracticeParticipation { get; init; }

    [JsonPropertyName("rotowire_id")]
    public int? RotowireId { get; init; }

    [JsonPropertyName("rotoworld_id")]
    public int? RotoworldId { get; init; }

    [JsonPropertyName("search_first_name")]
    public string? SearchFirstName { get; init; }

    [JsonPropertyName("search_full_name")]
    public string? SearchFullName { get; init; }

    [JsonPropertyName("search_last_name")]
    public string? SearchLastName { get; init; }

    [JsonPropertyName("search_rank")]
    public int? SearchRank { get; init; }

    [JsonPropertyName("sport")]
    public string? Sport { get; init; }

    [JsonPropertyName("sportradar_id")]
    public string? SportradarId { get; init; }

    [JsonPropertyName("stats_id")]
    public int? StatsId { get; init; }

    [JsonPropertyName("status")]
    public string? Status { get; init; }

    [JsonPropertyName("swish_id")]
    public int? SwishId { get; init; }

    [JsonPropertyName("team")]
    public string? Team { get; init; }

    [JsonPropertyName("team_abbr")]
    public string? TeamAbbr { get; init; }

    [JsonPropertyName("team_changed_at")]
    public string? TeamChangedAt { get; init; }

    [JsonPropertyName("weight")]
    public string? Weight { get; init; }

    [JsonPropertyName("yahoo_id")]
    public int? YahooId { get; init; }

    [JsonPropertyName("years_exp")]
    public int? YearsExp { get; init; }
}
