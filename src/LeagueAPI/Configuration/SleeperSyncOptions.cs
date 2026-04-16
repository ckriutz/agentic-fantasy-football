namespace LeagueAPI.Configuration;

public sealed class SleeperSyncOptions
{
    public const string SectionName = "SleeperSync";

    public string PlayersEndpoint { get; init; } = "https://api.sleeper.app/v1/players/nfl";

    public int DailySyncHourUtc { get; init; } = 6;

    public bool Enabled { get; init; } = true;

    public bool RunOnStartup { get; init; } = true;
}
