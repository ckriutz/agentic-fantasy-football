namespace LeagueAPI.Models;

public sealed class WaiverProcessRunEntity
{
    public Guid WaiverProcessRunId { get; set; }

    public int Season { get; set; }

    public int Week { get; set; }

    public required string Status { get; set; }

    public int ClaimsProcessed { get; set; }

    public int ClaimsSucceeded { get; set; }

    public int ClaimsFailed { get; set; }

    public string? ErrorMessage { get; set; }

    public DateTimeOffset StartedAtUtc { get; set; }

    public DateTimeOffset? CompletedAtUtc { get; set; }
}
