namespace LeagueAPI.Models;

public sealed class FantasyProsRankingPlayerEntity
{
    public int PlayerId { get; set; }

    public string? PlayerName { get; set; }

    public string? SportsDataId { get; set; }

    public string? PlayerTeamId { get; set; }

    public string? PlayerPositionId { get; set; }

    public string? PlayerPositions { get; set; }

    public string? PlayerShortName { get; set; }

    public string? PlayerEligibility { get; set; }

    public string? PlayerYahooPositions { get; set; }

    public string? PlayerPageUrl { get; set; }

    public string? PlayerFilename { get; set; }

    public string? PlayerYahooId { get; set; }

    public string? CbsPlayerId { get; set; }

    public string? PlayerByeWeek { get; set; }

    public decimal? PlayerOwnedAverage { get; set; }

    public decimal? PlayerOwnedEspn { get; set; }

    public decimal? PlayerOwnedYahoo { get; set; }

    public decimal? PlayerEcrDelta { get; set; }

    public int RankEcr { get; set; }

    public string? RankMinimum { get; set; }

    public string? RankMaximum { get; set; }

    public string? RankAverage { get; set; }

    public string? RankStandardDeviation { get; set; }

    public string? PositionRank { get; set; }

    public int Tier { get; set; }

    public int Season { get; set; }

    public int Week { get; set; }

    public DateTimeOffset RetrievedAtUtc { get; set; }

    public required string RawJson { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
