namespace LeagueAPI.Models;

public sealed class SleeperSnapshot
{
    public required string FileName { get; init; }

    public required string RelativePath { get; init; }

    public required string PayloadSha256 { get; init; }
}
