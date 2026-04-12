namespace PlayerDatabase.Configuration;

public sealed class SleeperSyncOptions
{
    public const string SectionName = "SleeperSync";

    public PlayerCatalogStorageMode Mode { get; init; } = PlayerCatalogStorageMode.SnapshotOnly;

    public string PlayersEndpoint { get; init; } = "https://api.sleeper.app/v1/players/nfl";

    public string SnapshotDirectory { get; init; } = "data/sleeper";

    public string StateFilePath { get; init; } = "data/sleeper/latest-sync.json";

    public int DailySyncHourUtc { get; init; } = 6;

    public bool Enabled { get; init; } = true;

    public bool RunOnStartup { get; init; } = true;
}

public enum PlayerCatalogStorageMode
{
    Auto,
    SnapshotOnly,
    SqlServer
}
