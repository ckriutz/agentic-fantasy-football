namespace LeagueAPI.Configuration;

public sealed class YahooSyncOptions
{
    public const string SectionName = "YahooSync";

    public bool Enabled { get; init; } = true;

    public string DefaultGameKey { get; init; } = "461";

    public int DefaultSeason { get; init; } = 2025;

    public int DefaultWeek { get; init; } = 1;

    public int PageSize { get; init; } = 25;

    public int DailySyncHourUtc { get; init; } = 6;

    public bool RunOnStartup { get; init; } = false;
}
