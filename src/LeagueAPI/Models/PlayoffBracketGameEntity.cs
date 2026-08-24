namespace LeagueAPI.Models;

public static class PlayoffRounds
{
    public const string WildCard = "wild_card";
    public const string Semifinal = "semifinal";
    public const string Championship = "championship";
    public const string ThirdPlace = "third_place";

    public static readonly string[] All = [WildCard, Semifinal, ThirdPlace, Championship];
}

public static class PlayoffGameStatuses
{
    public const string Pending = "pending";
    public const string Scheduled = "scheduled";
    public const string Complete = "complete";
}

public static class PlayoffParticipantSources
{
    public const string Winner = "winner";
    public const string Loser = "loser";
}

public sealed class PlayoffBracketGameEntity
{
    public int Id { get; set; }

    public int BracketId { get; set; }

    public required string Round { get; set; }

    public int GameSlot { get; set; }

    public int Week { get; set; }

    public int? HomeSeed { get; set; }

    public int? AwaySeed { get; set; }

    public string? HomeAgentId { get; set; }

    public string? AwayAgentId { get; set; }

    public int? HomeSourceGameId { get; set; }

    public string? HomeSourceOutcome { get; set; }

    public int? AwaySourceGameId { get; set; }

    public string? AwaySourceOutcome { get; set; }

    public int? MatchupId { get; set; }

    public string? WinnerAgentId { get; set; }

    public string? LoserAgentId { get; set; }

    public string Status { get; set; } = PlayoffGameStatuses.Pending;
}
