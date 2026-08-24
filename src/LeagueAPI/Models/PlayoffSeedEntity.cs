namespace LeagueAPI.Models;

public sealed class PlayoffSeedEntity
{
    public int Id { get; set; }

    public int BracketId { get; set; }

    public int Seed { get; set; }

    public required string AgentId { get; set; }

    public int Wins { get; set; }

    public int Losses { get; set; }

    public int Ties { get; set; }

    public decimal WinningPercentage { get; set; }

    public decimal PointsFor { get; set; }

    public decimal PointsAgainst { get; set; }
}
