namespace YahooDataSync.Configuration;

internal sealed class YahooSyncOptions
{
    public const string SectionName = "YahooSync";

    public bool Enabled { get; init; } = true;

    public bool RunOnStartup { get; init; }

    public int PageSize { get; init; } = 25;

    public int DailySyncHourUtc { get; init; } = 6;

    public int DailySyncMinuteUtc { get; init; } = 45;

    public string? GameKey { get; init; }
}
