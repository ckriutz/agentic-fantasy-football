using System.Text.Json;

namespace SleeperSync.Models;

public sealed record SleeperPlayersSnapshot(DateTimeOffset RetrievedAtUtc, JsonElement Players);
