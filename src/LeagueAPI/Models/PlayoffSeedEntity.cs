namespace LeagueAPI.Models;

public sealed class PlayoffSeedEntity
{
    public int Id { get; set; }

    public int BracketId { get; set; }

    public int Seed { get; set; }

    public required string AgentId { get; set; }
}
