namespace LeagueAPI.Models;

public sealed class YahooPlayerIdOverrideEntity
{
    public int YahooPlayerId { get; set; }

    public required string SleeperPlayerId { get; set; }

    public string? Note { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
