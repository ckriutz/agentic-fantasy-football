namespace LeagueAPI.Models;

public sealed record SleeperSnapshotImportRequest(string ContainerName, string BlobName, DateTimeOffset RetrievedAtUtc);
