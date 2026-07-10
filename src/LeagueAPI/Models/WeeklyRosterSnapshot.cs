namespace LeagueAPI.Models;

public sealed class WeeklyRosterSnapshot
{
    public long WeeklyRosterSnapshotId { get; set; }

    public int Season { get; set; }

    public int Week { get; set; }

    public required string AgentId { get; set; }

    public required string SleeperPlayerId { get; set; }

    public required string SlotType { get; set; }

    public bool IsStarter { get; set; }

    public DateTimeOffset FinalizedAtUtc { get; set; }
}
