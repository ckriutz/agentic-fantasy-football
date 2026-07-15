using System.Text.Json;

namespace LeagueAPI.Models;

public sealed record SleeperPlayersSnapshot(DateTimeOffset RetrievedAtUtc, JsonElement Players);
