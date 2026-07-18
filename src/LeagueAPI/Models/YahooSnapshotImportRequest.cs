namespace LeagueAPI.Models;

public sealed record YahooSnapshotImportRequest(string ContainerName, string BlobName, string GameKey, int Season, int Week, DateTimeOffset RetrievedAtUtc);
