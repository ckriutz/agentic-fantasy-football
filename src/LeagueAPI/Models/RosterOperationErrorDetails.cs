namespace LeagueAPI.Models;

public sealed class RosterOperationErrorDetails
{
    public string? AgentId { get; init; }

    public string? SleeperPlayerId { get; init; }

    public string? OwnerAgentId { get; init; }

    public string? PlayerPosition { get; init; }

    public string? AcquisitionSource { get; init; }

    public int? CurrentRosterSize { get; init; }

    public int? MaxRosterSize { get; init; }
}
