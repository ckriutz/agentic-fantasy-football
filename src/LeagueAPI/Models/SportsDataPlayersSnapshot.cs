namespace LeagueAPI.Models;

public sealed record SportsDataPlayersSnapshot(DateTimeOffset RetrievedAtUtc, IReadOnlyList<SportsDataFantasyPlayer> Players);
