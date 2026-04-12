namespace PlayerDatabase.Models;

public sealed class SleeperSyncState
{
    public Guid? SyncRunId { get; init; }

    public string Status { get; init; } = "NeverRun";

    public DateTimeOffset? LastAttemptedAtUtc { get; init; }

    public DateTimeOffset? LastSuccessfulSyncAtUtc { get; init; }

    public int? RecordCount { get; init; }

    public string? SnapshotFileName { get; init; }

    public string? SnapshotRelativePath { get; init; }

    public string? PayloadSha256 { get; init; }

    public string? ErrorMessage { get; init; }
}
