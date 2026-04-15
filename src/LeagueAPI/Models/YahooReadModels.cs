namespace LeagueAPI.Models;

public sealed record YahooWeeklyPlayerStatValueResult(
    int StatId,
    string? StatName,
    decimal Value);

public sealed record YahooWeeklyPlayerStatResult(
    string GameKey,
    int Season,
    int Week,
    int YahooPlayerId,
    string? SleeperPlayerId,
    string? FullName,
    string? Team,
    string? Position,
    string? EditorialTeamAbbr,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<YahooWeeklyPlayerStatValueResult> Stats);

public sealed record YahooWeeklyPlayerPointResult(
    string TemplateKey,
    int Season,
    int Week,
    int YahooPlayerId,
    string? SleeperPlayerId,
    string? FullName,
    string? Team,
    string? Position,
    decimal FantasyPoints,
    IReadOnlyDictionary<string, decimal>? Breakdown,
    DateTimeOffset CalculatedAtUtc);

public sealed record YahooSeasonPointWeekResult(
    int Week,
    decimal FantasyPoints,
    IReadOnlyDictionary<string, decimal>? Breakdown,
    DateTimeOffset CalculatedAtUtc);

public sealed record YahooPlayerSeasonPointsResult(
    string TemplateKey,
    int Season,
    int YahooPlayerId,
    string? SleeperPlayerId,
    string? FullName,
    string? Team,
    string? Position,
    int GamesCount,
    decimal TotalFantasyPoints,
    decimal AverageFantasyPoints,
    IReadOnlyList<YahooSeasonPointWeekResult> WeeklyPoints);

public sealed record ScoringTemplateRuleResult(
    int StatId,
    string? StatName,
    decimal Modifier);

public sealed record ScoringTemplateResult(
    string TemplateKey,
    string Name,
    string? Description,
    bool IsActive,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<ScoringTemplateRuleResult> Rules);
