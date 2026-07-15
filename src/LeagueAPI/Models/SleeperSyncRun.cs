namespace LeagueAPI.Models;

public sealed class SleeperSyncRun
{
    public Guid SyncRunId { get; set; }

    public string? ContainerName { get; set; }

    public string? BlobName { get; set; }

    public DateTimeOffset? RetrievedAtUtc { get; set; }

    public string? BlobETag { get; set; }

    public string? ContentHash { get; set; }

    public DateTimeOffset StartedAtUtc { get; set; }

    public DateTimeOffset? CompletedAtUtc { get; set; }

    public required string Status { get; set; }

    public int? RecordCount { get; set; }

    public bool AlreadyProcessed { get; set; }

    public string? ErrorMessage { get; set; }
}
