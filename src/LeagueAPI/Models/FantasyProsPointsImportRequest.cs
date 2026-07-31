namespace LeagueAPI.Models;

public sealed record FantasyProsPointsImportRequest(string ContainerName, string BlobName, int RequestedSeason, string ServedSeason, string ServedScoring, int EndWeek, DateTimeOffset RetrievedAtUtc);
