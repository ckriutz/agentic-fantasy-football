namespace LeagueAPI.Models;

public static class LeagueStateDefaults
{
    public const int SingletonId = 1;
    public const int DefaultSeason = 2025;
    public const int PreseasonWeek = 0;
    public const string DefaultPhase = LeagueStatePhases.FreeAgency;
    public const string DefaultSeasonStage = SeasonStages.RegularSeason;
    public const string DefaultUpdatedBy = LeagueStateUpdatedBy.Manual;
}

public static class LeagueStatePhases
{
    public const string Drafting = "drafting";
    public const string GamesLocked = "games_locked";
    public const string WaiverWindow = "waiver_window";
    public const string FreeAgency = "free_agency";
    public const string Complete = "complete";
}

public static class SeasonStages
{
    public const string Draft = "draft";
    public const string RegularSeason = "regular_season";
    public const string Playoffs = "playoffs";
    public const string Complete = "complete";
}

public static class LeagueStateUpdatedBy
{
    public const string Manual = "manual";
    public const string SeasonRunner = "season-runner";
    public const string WaiverProcessor = "waiver-processor";
}

public sealed record LeagueState(
    int Season,
    int Week,
    string Phase,
    string SeasonStage,
    DateTimeOffset UpdatedAtUtc,
    string UpdatedBy);
