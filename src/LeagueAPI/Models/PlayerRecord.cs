using System.Text.Json.Serialization;

namespace LeagueAPI.Models;

public sealed class PlayerRecord
{
    public required string SleeperPlayerId { get; init; }

    public int? YahooId { get; init; }

    public int? FantasyDataId { get; init; }

    public string? FullName { get; init; }

    public string? FirstName { get; init; }

    public string? LastName { get; init; }

    public string? Team { get; init; }

    public string? TeamAbbr { get; init; }

    public string? Position { get; init; }

    public int? SearchRank { get; init; }

    public string? InjuryStatus { get; init; }

    public string? InjuryNotes { get; init; }

    public string? Status { get; init; }

    public bool Active { get; init; }

    public required SleeperPlayer Data { get; init; }

    // SportsData enrichment fields
    public decimal? AverageDraftPosition { get; init; }

    public int? ByeWeek { get; init; }

    public decimal? LastSeasonFantasyPoints { get; init; }

    public decimal? ProjectedFantasyPoints { get; init; }

    public int? AuctionValue { get; init; }

    [JsonIgnore]
    public string SearchFullNameNormalized { get; init; } = string.Empty;

    [JsonIgnore]
    public string FantasyPositionsTokenized { get; init; } = string.Empty;

    [JsonIgnore]
    public string RawJson { get; init; } = string.Empty;
}
