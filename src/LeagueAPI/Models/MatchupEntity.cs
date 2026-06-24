namespace LeagueAPI.Models;

public sealed class MatchupEntity
{
    public int Id { get; set; }

    public int Week { get; set; }

    public required string HomeAgentId { get; set; }

    public required string AwayAgentId { get; set; }

    public decimal HomePoints { get; set; }

    public decimal AwayPoints { get; set; }

    public bool IsComplete { get; set; }
}
