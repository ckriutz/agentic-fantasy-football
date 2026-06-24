namespace LeagueAPI.Models;

public sealed record ScheduleMatchupResult(
    int MatchupId,
    int Week,
    string HomeAgentId,
    string AwayAgentId,
    decimal HomePoints,
    decimal AwayPoints,
    bool IsComplete);

public sealed record GenerateScheduleResult(
    bool Generated,
    string Message,
    int MatchupCount);

public sealed record WeeklyMatchupResult(
    int MatchupId,
    int Week,
    string AgentId,
    string OpponentAgentId,
    bool IsHomeTeam,
    decimal MyPoints,
    decimal OpponentPoints,
    bool IsComplete);
