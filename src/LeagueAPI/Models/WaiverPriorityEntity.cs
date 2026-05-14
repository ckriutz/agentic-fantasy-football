namespace LeagueAPI.Models;

public sealed class WaiverPriorityEntity
{
    public required string AgentId { get; set; }

    public int Priority { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
