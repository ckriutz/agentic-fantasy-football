namespace LeagueAPI.Models;

public sealed record ScheduleMatchupResult(
    int MatchupId,
    int Season,
    int Week,
    string MatchupType,
    string HomeAgentId,
    string AwayAgentId,
    decimal HomePoints,
    decimal AwayPoints,
    bool IsComplete,
    string? WinnerAgentId,
    bool IsTie);

public sealed record GenerateScheduleResult(
    int Season,
    bool Generated,
    string Message,
    int MatchupCount);

public sealed record AgentStanding(
    string AgentId,
    int Wins,
    int Losses,
    int Ties,
    decimal WinningPercentage,
    decimal PointsFor,
    decimal PointsAgainst);

public sealed record WeeklyMatchupResult(
    int MatchupId,
    int Season,
    int Week,
    string MatchupType,
    string AgentId,
    string OpponentAgentId,
    bool IsHomeTeam,
    decimal MyPoints,
    decimal OpponentPoints,
    bool IsComplete);
