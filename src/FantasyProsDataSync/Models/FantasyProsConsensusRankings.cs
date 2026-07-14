using System.Text.Json.Serialization;

namespace FantasyProsDataSync.Models;

public sealed record FantasyProsPlayersSnapshot(int Season, int Week, DateTimeOffset RetrievedAtUtc, IReadOnlyList<FantasyProsRankingPlayer> Players);

public sealed class FantasyProsLeagueState
{
    [JsonPropertyName("season")]
    public int Season { get; init; }

    [JsonPropertyName("week")]
    public int Week { get; init; }
}
