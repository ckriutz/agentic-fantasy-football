using System.Text.Json;

namespace YahooDataSync.Models;

internal sealed record LeagueState(int Season, int Week, string Phase, DateTimeOffset UpdatedAtUtc, string UpdatedBy);

internal sealed record YahooPlayersSnapshot(string GameKey, int Season, int Week, DateTimeOffset RetrievedAtUtc, List<JsonElement> Pages);

internal sealed record YahooSnapshotImportRequest(string ContainerName, string BlobName, string GameKey, int Season, int Week, DateTimeOffset RetrievedAtUtc);

internal sealed record YahooSyncResult(string ContainerName, string BlobName, string GameKey, int Season, int Week, DateTimeOffset RetrievedAtUtc, int PageCount);
