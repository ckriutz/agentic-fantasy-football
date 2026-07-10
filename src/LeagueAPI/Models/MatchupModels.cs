namespace LeagueAPI.Models;

public sealed record ScheduleMatchupResult(
    int MatchupId,
    int Week,
    string HomeAgentId,
    string AwayAgentId,
    decimal HomePoints,
    decimal AwayPoints,
    bool IsComplete,
    string? WinnerAgentId,
    bool IsTie);

public sealed record GenerateScheduleResult(
    bool Generated,
    string Message,
    int MatchupCount);

public sealed record AgentStanding(
    string AgentId,
    int Wins,
    int Losses,
    int Ties);

public sealed record WeeklyMatchupResult(
    int MatchupId,
    int Week,
    string AgentId,
    string OpponentAgentId,
    bool IsHomeTeam,
    decimal MyPoints,
    decimal OpponentPoints,
    bool IsComplete);
