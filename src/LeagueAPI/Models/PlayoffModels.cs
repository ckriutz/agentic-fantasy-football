namespace LeagueAPI.Models;

public sealed record PlayoffBracketResult(
    int Season,
    string Status,
    int RegularSeasonEndWeek,
    int PlayoffStartWeek,
    int ChampionshipWeek,
    int PlayoffTeamCount,
    int FirstRoundByeCount,
    IReadOnlyList<PlayoffSeedResult> Seeds,
    IReadOnlyList<PlayoffGameResult> Games);

public sealed record PlayoffSeedResult(
    int Seed,
    string AgentId,
    int Wins,
    int Losses,
    int Ties,
    decimal WinningPercentage,
    decimal PointsFor,
    decimal PointsAgainst,
    bool HasFirstRoundBye);

public sealed record PlayoffGameResult(
    string Round,
    int GameSlot,
    int Week,
    int? HomeSeed,
    int? AwaySeed,
    string? HomeAgentId,
    string? AwayAgentId,
    string? HomeSource,
    string? AwaySource,
    string Status,
    string? WinnerAgentId,
    string? LoserAgentId);
