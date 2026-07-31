namespace LeagueAPI.Models;

public sealed class FantasyProsScoreSyncRun
{
    public Guid SyncRunId { get; set; }

    public required string ContainerName { get; set; }

    public required string BlobName { get; set; }

    public int Season { get; set; }

    public int EndWeek { get; set; }

    public DateTimeOffset RetrievedAtUtc { get; set; }

    public string? BlobETag { get; set; }

    public string? ContentHash { get; set; }

    public DateTimeOffset StartedAtUtc { get; set; }

    public DateTimeOffset? CompletedAtUtc { get; set; }

    public required string Status { get; set; }

    public int? RecordCount { get; set; }

    public int? MatchedPlayerCount { get; set; }

    public int? UnmatchedPlayerCount { get; set; }

    public int? UnmatchedDstCount { get; set; }

    public string? ServedSeason { get; set; }

    public string? ServedScoring { get; set; }

    public bool AlreadyProcessed { get; set; }

    public string? ErrorMessage { get; set; }
}
