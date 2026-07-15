namespace FantasyProsDataSync.Models;

public sealed record FantasyProsSnapshotImportRequest(string ContainerName, string BlobName, int Season, int Week, DateTimeOffset RetrievedAtUtc);
