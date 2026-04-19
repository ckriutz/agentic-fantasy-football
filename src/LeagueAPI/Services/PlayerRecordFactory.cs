using System.Text.Json;
using LeagueAPI.Models;

namespace LeagueAPI.Services;

public static class PlayerRecordFactory
{
    private const string IgnoredSleeperFullName = "Duplicate Player";
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static PlayerRecord Create(string sleeperPlayerId, SleeperPlayer player)
    {
        var fantasyPositions =
            player.FantasyPositions?
                .Where(position => !string.IsNullOrWhiteSpace(position))
                .Select(NormalizeToken)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray()
            ?? Array.Empty<string>();

        return new PlayerRecord
        {
            SleeperPlayerId = sleeperPlayerId,
            YahooId = player.YahooId,
            FullName = player.FullName,
            FirstName = player.FirstName,
            LastName = player.LastName,
            Team = player.Team,
            TeamAbbr = player.TeamAbbr,
            Position = player.Position,
            FantasyPositions = fantasyPositions,
            Status = player.Status,
            Active = player.Active,
            Sport = player.Sport,
            SearchFullNameNormalized = NormalizeName(
                player.SearchFullName
                ?? player.FullName
                ?? $"{player.FirstName} {player.LastName}"),
            FantasyPositionsTokenized = BuildFantasyPositionsTokenized(fantasyPositions),
            RawJson = JsonSerializer.Serialize(player, SerializerOptions),
            Data = player,
            FantasyDataId = player.FantasyDataId
        };
    }

    public static PlayerRecord Map(PlayerEntity entity)
    {
        var sleeperPlayer =
            JsonSerializer.Deserialize<SleeperPlayer>(entity.RawJson)
            ?? throw new InvalidOperationException(
                $"Unable to deserialize stored player payload for {entity.SleeperPlayerId}.");

        return new PlayerRecord
        {
            SleeperPlayerId = entity.SleeperPlayerId,
            YahooId = entity.YahooId,
            FantasyDataId = entity.FantasyDataId,
            FullName = entity.FullName,
            FirstName = entity.FirstName,
            LastName = entity.LastName,
            Team = entity.Team,
            TeamAbbr = entity.TeamAbbr,
            Position = entity.Position,
            FantasyPositions = ParseFantasyPositions(entity.FantasyPositionsTokenized),
            Status = entity.Status,
            Active = entity.Active,
            Sport = entity.Sport,
            SearchFullNameNormalized = entity.SearchFullNameNormalized,
            FantasyPositionsTokenized = entity.FantasyPositionsTokenized,
            RawJson = entity.RawJson,
            Data = sleeperPlayer,
            AverageDraftPosition = entity.AverageDraftPosition,
            ByeWeek = entity.ByeWeek,
            LastSeasonFantasyPoints = entity.LastSeasonFantasyPoints,
            ProjectedFantasyPoints = entity.ProjectedFantasyPoints,
            AuctionValue = entity.AuctionValue
        };
    }

    public static bool ShouldIgnore(SleeperPlayer player)
    {
        return string.Equals(
            player.FullName?.Trim(),
            IgnoredSleeperFullName,
            StringComparison.OrdinalIgnoreCase);
    }

    public static bool ShouldIgnore(PlayerRecord player)
    {
        return string.Equals(
            player.FullName?.Trim(),
            IgnoredSleeperFullName,
            StringComparison.OrdinalIgnoreCase);
    }

    public static string NormalizeName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return string.Concat(
            value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToLowerInvariant();
    }

    public static string NormalizeToken(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToUpperInvariant();
    }

    private static string BuildFantasyPositionsTokenized(IEnumerable<string> positions)
    {
        var tokens = positions.Where(position => !string.IsNullOrWhiteSpace(position)).ToArray();
        return tokens.Length == 0 ? string.Empty : $"|{string.Join('|', tokens)}|";
    }

    private static IReadOnlyList<string> ParseFantasyPositions(string tokenizedFantasyPositions)
    {
        return tokenizedFantasyPositions.Split(
            '|',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
