namespace LeagueAPI.Models;

public sealed record LockPlayoffBracketResult(
    int Season,
    int BracketId,
    string Status,
    bool Created,
    IReadOnlyList<PlayoffSeedResult> Seeds,
    IReadOnlyList<PlayoffGameResult> Games);

public sealed record StageAwareFinalizeResult(
    MatchupScoreUpdateResult FinalizedWeek,
    bool LockedBracket,
    LockPlayoffBracketResult? Bracket);
