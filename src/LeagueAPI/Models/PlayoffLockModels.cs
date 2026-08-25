namespace LeagueAPI.Models;

public sealed record LockPlayoffBracketResult(
    int Season,
    int BracketId,
    string Status,
    bool Created,
    IReadOnlyList<PlayoffSeedResult> Seeds,
    IReadOnlyList<PlayoffGameResult> Games);

public sealed record ResolvePlayoffRoundResult(
    int Season,
    int Week,
    int BracketId,
    bool Advanced,
    bool Created,
    IReadOnlyList<PlayoffGameResult> CompletedGames,
    IReadOnlyList<PlayoffGameResult> ScheduledGames,
    bool SeasonCompleted,
    PlayoffFinalPlacementsResult? FinalPlacements);

public sealed record StageAwareFinalizeResult(
    MatchupScoreUpdateResult FinalizedWeek,
    bool LockedBracket,
    LockPlayoffBracketResult? Bracket,
    bool AdvancedRound,
    ResolvePlayoffRoundResult? Resolution,
    bool SeasonCompleted);
