using System.Text.Json.Serialization;

namespace LeagueAPI.Models;

public sealed class RosterToolPlayerResult
{
    public required string SleeperPlayerId { get; init; }

    public string? FullName { get; init; }

    public string? Team { get; init; }

    public string? Position { get; init; }

    public int? SearchRank { get; init; }

    public string? Status { get; init; }

    public string? InjuryStatus { get; init; }

    [JsonPropertyName("depth_chart_order")]
    public int? DepthChartOrder { get; init; }

    [JsonPropertyName("injury_body_part")]
    public string? InjuryBodyPart { get; init; }

    public int? ByeWeek { get; init; }

    public decimal? LastSeasonFantasyPoints { get; init; }

    public decimal? ProjectedFantasyPoints { get; init; }

    public int? AuctionValue { get; init; }

    public decimal? PlayerOwnedAverage { get; init; }

    public string? RankAverage { get; init; }

    public string? PositionRank { get; init; }

    public int? Tier { get; init; }

    public string? OwnerAgentId { get; init; }

    public bool IsAvailable { get; init; }

    public string? AcquisitionSource { get; init; }

    public string? SlotType { get; init; }

    public bool IsStarter { get; init; }

    public IReadOnlyDictionary<int, decimal> WeeklyPoints { get; init; } = RosterPlayerResult.EmptyWeeklyPoints;

    public PlayerLockStatus LockStatus { get; init; } = PlayerLockStatus.Unlocked;

    public static RosterToolPlayerResult FromRosterPlayerResult(RosterPlayerResult rosterPlayer)
    {
        return new RosterToolPlayerResult
        {
            SleeperPlayerId = rosterPlayer.Player.SleeperPlayerId,
            FullName = rosterPlayer.Player.FullName,
            Team = rosterPlayer.Player.Team,
            Position = rosterPlayer.Player.Position,
            SearchRank = rosterPlayer.Player.SearchRank,
            Status = rosterPlayer.Player.Status,
            InjuryStatus = rosterPlayer.Player.InjuryStatus,
            DepthChartOrder = rosterPlayer.Player.Data.DepthChartOrder,
            InjuryBodyPart = rosterPlayer.Player.Data.InjuryBodyPart,
            ByeWeek = rosterPlayer.Player.ByeWeek,
            LastSeasonFantasyPoints = rosterPlayer.Player.LastSeasonFantasyPoints,
            ProjectedFantasyPoints = rosterPlayer.Player.ProjectedFantasyPoints,
            AuctionValue = rosterPlayer.Player.AuctionValue,
            PlayerOwnedAverage = rosterPlayer.Player.PlayerOwnedAverage,
            RankAverage = rosterPlayer.Player.RankAverage,
            PositionRank = rosterPlayer.Player.PositionRank,
            Tier = rosterPlayer.Player.Tier,
            OwnerAgentId = rosterPlayer.OwnerAgentId,
            IsAvailable = rosterPlayer.IsAvailable,
            AcquisitionSource = rosterPlayer.AcquisitionSource,
            SlotType = rosterPlayer.SlotType,
            IsStarter = rosterPlayer.IsStarter,
            WeeklyPoints = rosterPlayer.WeeklyPoints,
            LockStatus = rosterPlayer.LockStatus
        };
    }
}
