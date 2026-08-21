namespace LeagueAPI.Models;

public sealed class LeagueStateEntity
{
    public int Id { get; set; } = LeagueStateDefaults.SingletonId;

    public int Season { get; set; } = LeagueStateDefaults.DefaultSeason;

    public int Week { get; set; } = LeagueStateDefaults.PreseasonWeek;

    public string Phase { get; set; } = LeagueStateDefaults.DefaultPhase;

    public string SeasonStage { get; set; } = LeagueStateDefaults.DefaultSeasonStage;

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public string UpdatedBy { get; set; } = LeagueStateDefaults.DefaultUpdatedBy;
}
