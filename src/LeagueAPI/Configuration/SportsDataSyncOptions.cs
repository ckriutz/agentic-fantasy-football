namespace LeagueAPI.Configuration;

public sealed class SportsDataSyncOptions
{
    public const string SectionName = "SportsDataSync";

    public bool Enabled { get; init; } = true;

    public bool RunOnStartup { get; init; } = true;

    public int DailySyncHourUtc { get; init; } = 7;

    public string BaseUrl { get; init; } = "https://api.sportsdata.io/v3/nfl/stats/json";

    public string FantasyPlayersEndpoint { get; init; } = "FantasyPlayers";

    public string ApiKey { get; init; } = string.Empty;
}
