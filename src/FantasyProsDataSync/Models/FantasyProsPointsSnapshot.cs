namespace FantasyProsDataSync.Models;

public sealed record FantasyProsPointsSnapshot(string Season, string Scoring, DateTimeOffset RetrievedAtUtc, IReadOnlyList<FantasyProsPlayerPoints> Players);
