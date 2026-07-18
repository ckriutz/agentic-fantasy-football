using System.Text.Json;

namespace LeagueAPI.Models;

public sealed record YahooPlayersSnapshot(string GameKey, int Season, int Week, DateTimeOffset RetrievedAtUtc, List<JsonElement> Pages);
