namespace LeagueAPI.Models;

public sealed class PlayerQuery
{
    public string? Name { get; init; }

    public string? Team { get; init; }

    public string? Position { get; init; }

    public int? ByeWeek { get; init; }

    public decimal? MinProjectedPoints { get; init; }

    public decimal? MaxAverageDraftPosition { get; init; }

    public string? SortBy { get; init; }

    public bool SortDescending { get; init; }

    public int Limit { get; init; } = 25;
}
