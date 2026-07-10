namespace LeagueAPI.Models;

public sealed record MatchupScoreUpdateResult(
    int Season,
    int Week,
    int MatchupCount,
    bool IsFinalized,
    DateTimeOffset CalculatedAtUtc);
