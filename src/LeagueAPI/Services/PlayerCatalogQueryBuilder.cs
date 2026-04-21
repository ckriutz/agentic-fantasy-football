using LeagueAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace LeagueAPI.Services;

internal static class PlayerCatalogQueryBuilder
{
    public static IQueryable<PlayerEntity> ApplyFilters(IQueryable<PlayerEntity> playersQuery, PlayerQuery query)
    {
        var normalizedName = PlayerRecordFactory.NormalizeName(query.Name);
        var normalizedTeam = PlayerRecordFactory.NormalizeToken(query.Team);
        var normalizedPosition = PlayerRecordFactory.NormalizeToken(query.Position);

        if (!string.IsNullOrWhiteSpace(normalizedName))
        {
            playersQuery = playersQuery.Where(entity =>
                EF.Functions.Like(entity.SearchFullNameNormalized, $"%{normalizedName}%"));
        }

        if (!string.IsNullOrWhiteSpace(normalizedTeam))
        {
            playersQuery = playersQuery.Where(entity =>
                (entity.TeamAbbr ?? string.Empty).ToUpper() == normalizedTeam
                || (entity.Team ?? string.Empty).ToUpper() == normalizedTeam);
        }

        if (!string.IsNullOrWhiteSpace(normalizedPosition))
        {
            playersQuery = playersQuery.Where(entity =>
                (entity.Position ?? string.Empty).ToUpper() == normalizedPosition
                || EF.Functions.Like(entity.FantasyPositionsTokenized, $"%|{normalizedPosition}|%"));
        }

        if (query.ByeWeek.HasValue)
        {
            playersQuery = playersQuery.Where(entity => entity.ByeWeek == query.ByeWeek.Value);
        }

        return playersQuery;
    }

    public static IOrderedQueryable<PlayerEntity> ApplyOrdering(IQueryable<PlayerEntity> playersQuery, PlayerQuery query)
    {
        return query.SortBy?.ToLowerInvariant() switch
        {
            "projectedpoints" => query.SortDescending
                ? playersQuery.OrderByDescending(entity => entity.ProjectedFantasyPoints)
                : playersQuery.OrderBy(entity => entity.ProjectedFantasyPoints),
            "adp" => query.SortDescending
                ? playersQuery.OrderByDescending(entity => entity.AverageDraftPosition)
                : playersQuery.OrderBy(entity => entity.AverageDraftPosition),
            "lastseasonpoints" => query.SortDescending
                ? playersQuery.OrderByDescending(entity => entity.LastSeasonFantasyPoints)
                : playersQuery.OrderBy(entity => entity.LastSeasonFantasyPoints),
            "auctionvalue" => query.SortDescending
                ? playersQuery.OrderByDescending(entity => entity.AuctionValue)
                : playersQuery.OrderBy(entity => entity.AuctionValue),
            _ => query.SortDescending
                ? playersQuery.OrderByDescending(entity => entity.FullName ?? entity.SleeperPlayerId)
                : playersQuery.OrderBy(entity => entity.FullName ?? entity.SleeperPlayerId)
        };
    }

    public static int NormalizeLimit(int limit)
    {
        return Math.Clamp(limit, 1, 200);
    }
}
