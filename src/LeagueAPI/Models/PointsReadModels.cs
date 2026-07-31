namespace LeagueAPI.Models;

public sealed record WeeklyPlayerScoreResult(
    int Season,
    int Week,
    int FantasyProsPlayerId,
    string? SleeperPlayerId,
    string? PlayerName,
    string? PositionId,
    string? TeamId,
    decimal Points,
    DateTimeOffset UpdatedAtUtc);

public sealed record SeasonPointWeekResult(
    int Week,
    decimal Points,
    DateTimeOffset UpdatedAtUtc);

public sealed record PlayerSeasonPointsResult(
    int Season,
    string SleeperPlayerId,
    string? PlayerName,
    string? PositionId,
    string? TeamId,
    int GamesCount,
    decimal TotalPoints,
    decimal AveragePoints,
    IReadOnlyList<SeasonPointWeekResult> WeeklyPoints);
