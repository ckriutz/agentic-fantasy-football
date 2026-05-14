namespace LeagueAPI.Models;

public sealed class WaiverClaimEntity
{
    public Guid WaiverClaimId { get; set; }

    public required string AgentId { get; set; }

    public int Season { get; set; }

    public int Week { get; set; }

    public int ClaimOrder { get; set; }

    public required string AddSleeperPlayerId { get; set; }

    public required string DropSleeperPlayerId { get; set; }

    public int PriorityAtSubmission { get; set; }

    public required string Status { get; set; }

    public string? FailureReason { get; set; }

    public DateTimeOffset SubmittedAtUtc { get; set; }

    public DateTimeOffset? ProcessedAtUtc { get; set; }
}
