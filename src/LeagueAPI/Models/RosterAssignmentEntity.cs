namespace LeagueAPI.Models;

public sealed class RosterAssignmentEntity
{
    public Guid RosterAssignmentId { get; set; }

    public required string AgentId { get; set; }

    public required string SleeperPlayerId { get; set; }

    public DateTimeOffset AcquiredAtUtc { get; set; }

    public required string AcquisitionSource { get; set; }
}
