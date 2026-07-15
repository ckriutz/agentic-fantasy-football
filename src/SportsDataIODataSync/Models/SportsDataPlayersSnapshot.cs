namespace SportsDataIODataSync.Models;

public sealed record SportsDataPlayersSnapshot(DateTimeOffset RetrievedAtUtc, IReadOnlyList<SportsDataFantasyPlayer> Players);
