using System.Globalization;
using LeagueAPI.Data;
using Microsoft.EntityFrameworkCore;

namespace LeagueAPI.Services;

/// <summary>
/// Shared FantasyPros -> Sleeper player id resolution.
/// Tier order: YahooId, SportradarId (case-insensitive), DST team code.
/// </summary>
public static class FantasyProsPlayerBridge
{
    public sealed record Identity(string? PlayerYahooId, string? SportsDataId, string? PositionId, string? TeamId);

    public sealed class LookupMaps
    {
        public required IReadOnlyDictionary<int, string> SleeperByYahooId { get; init; }
        public required IReadOnlyDictionary<string, string> SleeperBySportradarId { get; init; }
        public required IReadOnlyDictionary<string, string> DstSleeperByTeamId { get; init; }
    }

    public static async Task<LookupMaps> LoadMapsAsync(LeagueApiDbContext dbContext, IEnumerable<Identity> identities, CancellationToken cancellationToken)
    {
        var identityList = identities as IList<Identity> ?? identities.ToList();

        var yahooIds = identityList
            .Select(identity => int.TryParse(identity.PlayerYahooId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var yahooId) ? yahooId : (int?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToArray();

        var sportradarIds = identityList
            .Select(identity => identity.SportsDataId?.Trim())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Dictionary<int, string> sleeperByYahooId;
        if (yahooIds.Length == 0)
        {
            sleeperByYahooId = new Dictionary<int, string>();
        }
        else
        {
            var yahooMatches = await dbContext.Players.AsNoTracking()
                .Where(player => player.YahooId != null && yahooIds.Contains(player.YahooId.Value))
                .Select(player => new { YahooId = player.YahooId!.Value, player.SleeperPlayerId })
                .ToListAsync(cancellationToken);
            sleeperByYahooId = yahooMatches
                .GroupBy(player => player.YahooId)
                .ToDictionary(group => group.Key, group => group.First().SleeperPlayerId);
        }

        Dictionary<string, string> sleeperBySportradarId;
        if (sportradarIds.Length == 0)
        {
            sleeperBySportradarId = new Dictionary<string, string>(StringComparer.Ordinal);
        }
        else
        {
            var lowerSportradarIds = sportradarIds.Select(id => id.ToLowerInvariant()).ToArray();
            var sportradarMatches = await dbContext.Players.AsNoTracking()
                .Where(player => player.SportradarId != null && lowerSportradarIds.Contains(player.SportradarId.ToLower()))
                .Select(player => new { player.SportradarId, player.SleeperPlayerId })
                .ToListAsync(cancellationToken);
            sleeperBySportradarId = sportradarMatches
                .Where(player => !string.IsNullOrWhiteSpace(player.SportradarId))
                .GroupBy(player => player.SportradarId!.Trim().ToLowerInvariant())
                .ToDictionary(group => group.Key, group => group.First().SleeperPlayerId);
        }

        // Sleeper team defenses use SleeperPlayerId = team code (e.g. "ARI") with Position = "DEF".
        var dstMatches = await dbContext.Players.AsNoTracking()
            .Where(player => player.Position == "DEF")
            .Select(player => player.SleeperPlayerId)
            .ToListAsync(cancellationToken);
        var dstSleeperByTeamId = dstMatches
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .GroupBy(id => id.Trim().ToUpperInvariant())
            .ToDictionary(group => group.Key, group => group.First());

        return new LookupMaps
        {
            SleeperByYahooId = sleeperByYahooId,
            SleeperBySportradarId = sleeperBySportradarId,
            DstSleeperByTeamId = dstSleeperByTeamId
        };
    }

    public static string? ResolveSleeperPlayerId(Identity identity, LookupMaps maps)
    {
        if (int.TryParse(identity.PlayerYahooId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var yahooId)
            && maps.SleeperByYahooId.TryGetValue(yahooId, out var sleeperByYahoo))
        {
            return sleeperByYahoo;
        }

        var sportsDataId = identity.SportsDataId?.Trim();
        if (!string.IsNullOrWhiteSpace(sportsDataId)
            && maps.SleeperBySportradarId.TryGetValue(sportsDataId.ToLowerInvariant(), out var sleeperBySportradar))
        {
            return sleeperBySportradar;
        }

        if (IsDstPosition(identity.PositionId))
        {
            var teamId = identity.TeamId?.Trim();
            if (!string.IsNullOrWhiteSpace(teamId)
                && maps.DstSleeperByTeamId.TryGetValue(teamId.ToUpperInvariant(), out var sleeperByTeam))
            {
                return sleeperByTeam;
            }
        }

        return null;
    }

    public static bool IsDstPosition(string? positionId)
    {
        return string.Equals(positionId, "DST", StringComparison.OrdinalIgnoreCase)
            || string.Equals(positionId, "DEF", StringComparison.OrdinalIgnoreCase);
    }
}
