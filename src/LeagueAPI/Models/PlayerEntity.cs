namespace LeagueAPI.Models;

public sealed class PlayerEntity
{
    public required string SleeperPlayerId { get; set; }

    public int? YahooId { get; set; }

    public int? FantasyDataId { get; set; }

    public string? FullName { get; set; }

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public required string SearchFullNameNormalized { get; set; }

    public string? Team { get; set; }

    public string? TeamAbbr { get; set; }

    public string? Position { get; set; }

    public required string FantasyPositionsTokenized { get; set; }

    public string? Status { get; set; }

    public bool Active { get; set; }

    public string? Sport { get; set; }

    public required string RawJson { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    // SportsData enrichment fields (populated by SportsData sync)
    public decimal? AverageDraftPosition { get; set; }

    public int? ByeWeek { get; set; }

    public decimal? LastSeasonFantasyPoints { get; set; }

    public decimal? ProjectedFantasyPoints { get; set; }

    public int? AuctionValue { get; set; }
}
