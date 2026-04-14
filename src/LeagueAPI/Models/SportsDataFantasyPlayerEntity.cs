namespace LeagueAPI.Models;

public sealed class SportsDataFantasyPlayerEntity
{
    public int SportsDataPlayerId { get; set; }

    public string? Name { get; set; }

    public string? Team { get; set; }

    public string? Position { get; set; }

    public string? FantasyPlayerKey { get; set; }

    public decimal? AverageDraftPosition { get; set; }

    public decimal? AverageDraftPositionPPR { get; set; }

    public int? ByeWeek { get; set; }

    public decimal? LastSeasonFantasyPoints { get; set; }

    public decimal? ProjectedFantasyPoints { get; set; }

    public int? AuctionValue { get; set; }

    public int? AuctionValuePPR { get; set; }

    public decimal? AverageDraftPositionIDP { get; set; }

    public decimal? AverageDraftPositionRookie { get; set; }

    public decimal? AverageDraftPositionDynasty { get; set; }

    public decimal? AverageDraftPosition2QB { get; set; }

    public required string RawJson { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
