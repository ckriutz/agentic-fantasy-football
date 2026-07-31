namespace FantasyProsDataSync.Models;

public sealed record FantasyProsPointsSyncResult(string ContainerName, string BlobName, int RequestedSeason, string ServedSeason, int EndWeek, int PlayerCount, DateTimeOffset RetrievedAtUtc);
