namespace LeagueAPI.Models;

public static class PlayoffParticipantStatuses
{
    public const string Active = "active";
    public const string Eliminated = "eliminated";
}

public static class PlayoffEliminationReasons
{
    public const string RegularSeason = "regular_season";
    public const string InContention = "in_contention";
    public const string NotInPlayoffs = "not_in_playoffs";
    public const string WildCardLoss = "wild_card_loss";
    public const string SemifinalLoss = "semifinal_loss";
    public const string ThirdPlaceLoss = "third_place_loss";
    public const string ChampionshipLoss = "championship_loss";
}

public sealed record PlayoffEligibilityResult(
    int Season,
    bool BracketLocked,
    string? BracketStatus,
    int RegularSeasonEndWeek,
    int PlayoffStartWeek,
    int ChampionshipWeek,
    bool ThirdPlaceGameEnabled,
    IReadOnlyList<string> ActiveAgentIds,
    IReadOnlyList<string> EliminatedAgentIds,
    IReadOnlyList<PlayoffAgentStatusResult> Agents);

public sealed record PlayoffAgentStatusResult(
    string AgentId,
    string Status,
    string Reason,
    int? Seed,
    bool IsPlayoffTeam,
    string? EliminatedRound,
    int? EliminatedWeek);
