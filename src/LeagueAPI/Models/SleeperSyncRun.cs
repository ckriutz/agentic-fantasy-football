namespace LeagueAPI.Models;

public sealed class SleeperSyncRun
{
    public Guid SyncRunId { get; set; }

    public DateTimeOffset StartedAtUtc { get; set; }

    public DateTimeOffset? CompletedAtUtc { get; set; }

    public required string Status { get; set; }

    public int? RecordCount { get; set; }

    public string? ErrorMessage { get; set; }
}
