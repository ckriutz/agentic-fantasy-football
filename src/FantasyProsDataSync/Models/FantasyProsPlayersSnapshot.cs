using System.Text.Json.Serialization;

namespace FantasyProsDataSync.Models;

public sealed record FantasyProsPlayersSnapshot(int Season, int Week, DateTimeOffset RetrievedAtUtc, IReadOnlyList<FantasyProsRankingPlayer> Players);