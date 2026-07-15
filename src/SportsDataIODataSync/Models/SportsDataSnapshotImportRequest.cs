namespace SportsDataIODataSync.Models;

public sealed record SportsDataSnapshotImportRequest(string ContainerName, string BlobName, DateTimeOffset RetrievedAtUtc);
