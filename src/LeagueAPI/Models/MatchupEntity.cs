namespace LeagueAPI.Models;

public static class MatchupTypes
{
    public const string RegularSeason = "regular_season";
    public const string Playoff = "playoff";
}

public sealed class MatchupEntity
{
    public int Id { get; set; }

    public int Season { get; set; } = LeagueStateDefaults.DefaultSeason;

    public int Week { get; set; }

    public string MatchupType { get; set; } = MatchupTypes.RegularSeason;

    public required string HomeAgentId { get; set; }

    public required string AwayAgentId { get; set; }

    public decimal HomePoints { get; set; }

    public decimal AwayPoints { get; set; }

    public bool IsComplete { get; set; }

    public string? WinnerAgentId { get; set; }

    public bool IsTie { get; set; }
}
