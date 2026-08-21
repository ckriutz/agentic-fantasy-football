namespace LeagueAPI.Models;

public static class PlayoffSettingsDefaults
{
    public const int SingletonId = 1;
    public const int RegularSeasonEndWeek = 14;
    public const int PlayoffStartWeek = 15;
    public const int ChampionshipWeek = 17;
    public const int PlayoffTeamCount = 6;
    public const int FirstRoundByeCount = 2;
    public const bool Reseed = false;
    public const string PlayoffTieResolution = PlayoffTieResolutions.HigherSeed;
    public const bool ThirdPlaceGameEnabled = true;
}

public static class PlayoffTieResolutions
{
    public const string HigherSeed = "higher_seed";
}

public sealed class PlayoffSettingsEntity
{
    public int Id { get; set; } = PlayoffSettingsDefaults.SingletonId;

    public int RegularSeasonEndWeek { get; set; } = PlayoffSettingsDefaults.RegularSeasonEndWeek;

    public int PlayoffStartWeek { get; set; } = PlayoffSettingsDefaults.PlayoffStartWeek;

    public int ChampionshipWeek { get; set; } = PlayoffSettingsDefaults.ChampionshipWeek;

    public int PlayoffTeamCount { get; set; } = PlayoffSettingsDefaults.PlayoffTeamCount;

    public int FirstRoundByeCount { get; set; } = PlayoffSettingsDefaults.FirstRoundByeCount;

    public bool Reseed { get; set; } = PlayoffSettingsDefaults.Reseed;

    public string PlayoffTieResolution { get; set; } = PlayoffSettingsDefaults.PlayoffTieResolution;

    public bool ThirdPlaceGameEnabled { get; set; } = PlayoffSettingsDefaults.ThirdPlaceGameEnabled;
}
