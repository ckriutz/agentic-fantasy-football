namespace LeagueAPI.Models;

public sealed class DecisionEntity
{
    public int DecisionId { get; set; }

    public required string AgentId { get; set; }

    public int Week { get; set; }

    public required string Type { get; set; }

    public required string Reasoning { get; set; }

    public required string Action { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
