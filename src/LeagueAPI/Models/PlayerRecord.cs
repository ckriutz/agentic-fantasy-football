using System.Text.Json.Serialization;

namespace LeagueAPI.Models;

public sealed class PlayerRecord
{
    public required string SleeperPlayerId { get; init; }

    public int? YahooId { get; init; }

    public string? FullName { get; init; }

    public string? FirstName { get; init; }

    public string? LastName { get; init; }

    public string? Team { get; init; }

    public string? TeamAbbr { get; init; }

    public string? Position { get; init; }

    public IReadOnlyList<string> FantasyPositions { get; init; } = Array.Empty<string>();

    public string? Status { get; init; }

    public bool Active { get; init; }

    public string? Sport { get; init; }

    public required SleeperPlayer Data { get; init; }

    [JsonIgnore]
    public string SearchFullNameNormalized { get; init; } = string.Empty;

    [JsonIgnore]
    public string FantasyPositionsTokenized { get; init; } = string.Empty;

    [JsonIgnore]
    public string RawJson { get; init; } = string.Empty;
}
