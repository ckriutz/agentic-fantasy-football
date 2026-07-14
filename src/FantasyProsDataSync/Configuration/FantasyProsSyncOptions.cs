namespace FantasyProsDataSync.Configuration;

public sealed class FantasyProsSyncOptions
{
    public const string SectionName = "FantasyProsSync";

    public int ScheduleHour { get; init; } = 6;

    public int ScheduleMinute { get; init; } = 30;

    public string TimeZoneId { get; init; } = "America/New_York";

    public string ApiBaseUrl { get; init; } = "https://api.fantasypros.com/public/v2/json";

    public string LeagueApiBaseUrl { get; init; } = "http://localhost:5000";

    public string ApiKey { get; set; } = string.Empty;

    public string AzureStorageConnectionString { get; set; } = string.Empty;

    public string BlobContainerName { get; set; } = "fantasyprosdata";

    public int RequestTimeoutSeconds { get; init; } = 30;
}
