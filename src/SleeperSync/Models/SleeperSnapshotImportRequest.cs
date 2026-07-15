namespace SleeperSync.Models;

public sealed record SleeperSnapshotImportRequest(string ContainerName, string BlobName, DateTimeOffset RetrievedAtUtc);
