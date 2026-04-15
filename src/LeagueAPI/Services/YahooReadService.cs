using System.Text.Json;
using LeagueAPI.Data;
using LeagueAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace LeagueAPI.Services;

public sealed class YahooReadService(IDbContextFactory<LeagueApiDbContext> dbContextFactory)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IDbContextFactory<LeagueApiDbContext> _dbContextFactory = dbContextFactory;

    public async Task<IReadOnlyList<YahooWeeklyPlayerStatResult>> GetWeeklyStatsAsync(
        int season,
        int week,
        string? position,
        int limit,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        EnsureDatabaseConfigured(dbContext);

        var normalizedPosition = NormalizePosition(position);
        var normalizedLimit = NormalizeLimit(limit);

        var query = dbContext.WeeklyPlayerStats
            .AsNoTracking()
            .Where(playerStat => playerStat.Season == season && playerStat.Week == week)
            .Include(playerStat => playerStat.StatValues)
            .AsQueryable();

        if (normalizedPosition is not null)
        {
            query = query.Where(playerStat => playerStat.Position == normalizedPosition);
        }

        var playerStats = await query
            .OrderBy(playerStat => playerStat.Position)
            .ThenBy(playerStat => playerStat.FullName)
            .Take(normalizedLimit)
            .ToListAsync(cancellationToken);

        return playerStats.Select(MapWeeklyStat).ToList();
    }

    public async Task<YahooWeeklyPlayerStatResult?> GetPlayerWeeklyStatsBySleeperIdAsync(
        string sleeperPlayerId,
        int season,
        int week,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        EnsureDatabaseConfigured(dbContext);

        var playerStat = await dbContext.WeeklyPlayerStats
            .AsNoTracking()
            .Include(stat => stat.StatValues)
            .Where(stat =>
                stat.SleeperPlayerId == sleeperPlayerId
                && stat.Season == season
                && stat.Week == week)
            .FirstOrDefaultAsync(cancellationToken);

        return playerStat is null ? null : MapWeeklyStat(playerStat);
    }

    public async Task<YahooWeeklyPlayerStatResult?> GetPlayerWeeklyStatsByYahooIdAsync(
        int yahooPlayerId,
        int season,
        int week,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        EnsureDatabaseConfigured(dbContext);

        var playerStat = await dbContext.WeeklyPlayerStats
            .AsNoTracking()
            .Include(stat => stat.StatValues)
            .Where(stat =>
                stat.YahooPlayerId == yahooPlayerId
                && stat.Season == season
                && stat.Week == week)
            .FirstOrDefaultAsync(cancellationToken);

        return playerStat is null ? null : MapWeeklyStat(playerStat);
    }

    public async Task<IReadOnlyList<YahooWeeklyPlayerPointResult>> GetWeeklyPointsAsync(
        int season,
        int week,
        string? templateKey,
        string? position,
        int limit,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        EnsureDatabaseConfigured(dbContext);

        var resolvedTemplateKey = await ResolveTemplateKeyAsync(dbContext, templateKey, cancellationToken);
        if (resolvedTemplateKey is null)
        {
            return [];
        }

        var normalizedPosition = NormalizePosition(position);
        var normalizedLimit = NormalizeLimit(limit);

        var query = dbContext.WeeklyPlayerPoints
            .AsNoTracking()
            .Include(playerPoint => playerPoint.WeeklyPlayerStat)
            .Where(playerPoint =>
                playerPoint.TemplateKey == resolvedTemplateKey
                && playerPoint.WeeklyPlayerStat.Season == season
                && playerPoint.WeeklyPlayerStat.Week == week)
            .AsQueryable();

        if (normalizedPosition is not null)
        {
            query = query.Where(playerPoint => playerPoint.WeeklyPlayerStat.Position == normalizedPosition);
        }

        var playerPoints = await query
            .OrderByDescending(playerPoint => playerPoint.FantasyPoints)
            .ThenBy(playerPoint => playerPoint.WeeklyPlayerStat.FullName)
            .Take(normalizedLimit)
            .ToListAsync(cancellationToken);

        return playerPoints.Select(MapWeeklyPoint).ToList();
    }

    public async Task<YahooWeeklyPlayerPointResult?> GetPlayerWeeklyPointsBySleeperIdAsync(
        string sleeperPlayerId,
        int season,
        int week,
        string? templateKey,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        EnsureDatabaseConfigured(dbContext);

        var resolvedTemplateKey = await ResolveTemplateKeyAsync(dbContext, templateKey, cancellationToken);
        if (resolvedTemplateKey is null)
        {
            return null;
        }

        var playerPoint = await dbContext.WeeklyPlayerPoints
            .AsNoTracking()
            .Include(point => point.WeeklyPlayerStat)
            .Where(point =>
                point.TemplateKey == resolvedTemplateKey
                && point.WeeklyPlayerStat.SleeperPlayerId == sleeperPlayerId
                && point.WeeklyPlayerStat.Season == season
                && point.WeeklyPlayerStat.Week == week)
            .FirstOrDefaultAsync(cancellationToken);

        return playerPoint is null ? null : MapWeeklyPoint(playerPoint);
    }

    public async Task<YahooWeeklyPlayerPointResult?> GetPlayerWeeklyPointsByYahooIdAsync(
        int yahooPlayerId,
        int season,
        int week,
        string? templateKey,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        EnsureDatabaseConfigured(dbContext);

        var resolvedTemplateKey = await ResolveTemplateKeyAsync(dbContext, templateKey, cancellationToken);
        if (resolvedTemplateKey is null)
        {
            return null;
        }

        var playerPoint = await dbContext.WeeklyPlayerPoints
            .AsNoTracking()
            .Include(point => point.WeeklyPlayerStat)
            .Where(point =>
                point.TemplateKey == resolvedTemplateKey
                && point.WeeklyPlayerStat.YahooPlayerId == yahooPlayerId
                && point.WeeklyPlayerStat.Season == season
                && point.WeeklyPlayerStat.Week == week)
            .FirstOrDefaultAsync(cancellationToken);

        return playerPoint is null ? null : MapWeeklyPoint(playerPoint);
    }

    public async Task<YahooPlayerSeasonPointsResult?> GetPlayerSeasonPointsBySleeperIdAsync(
        string sleeperPlayerId,
        int season,
        string? templateKey,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        EnsureDatabaseConfigured(dbContext);

        var resolvedTemplateKey = await ResolveTemplateKeyAsync(dbContext, templateKey, cancellationToken);
        if (resolvedTemplateKey is null)
        {
            return null;
        }

        var playerPoints = await dbContext.WeeklyPlayerPoints
            .AsNoTracking()
            .Include(point => point.WeeklyPlayerStat)
            .Where(point =>
                point.TemplateKey == resolvedTemplateKey
                && point.WeeklyPlayerStat.SleeperPlayerId == sleeperPlayerId
                && point.WeeklyPlayerStat.Season == season)
            .OrderBy(point => point.WeeklyPlayerStat.Week)
            .ToListAsync(cancellationToken);

        return playerPoints.Count == 0 ? null : MapSeasonPoints(playerPoints);
    }

    public async Task<YahooPlayerSeasonPointsResult?> GetPlayerSeasonPointsByYahooIdAsync(
        int yahooPlayerId,
        int season,
        string? templateKey,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        EnsureDatabaseConfigured(dbContext);

        var resolvedTemplateKey = await ResolveTemplateKeyAsync(dbContext, templateKey, cancellationToken);
        if (resolvedTemplateKey is null)
        {
            return null;
        }

        var playerPoints = await dbContext.WeeklyPlayerPoints
            .AsNoTracking()
            .Include(point => point.WeeklyPlayerStat)
            .Where(point =>
                point.TemplateKey == resolvedTemplateKey
                && point.WeeklyPlayerStat.YahooPlayerId == yahooPlayerId
                && point.WeeklyPlayerStat.Season == season)
            .OrderBy(point => point.WeeklyPlayerStat.Week)
            .ToListAsync(cancellationToken);

        return playerPoints.Count == 0 ? null : MapSeasonPoints(playerPoints);
    }

    public async Task<IReadOnlyList<ScoringTemplateResult>> GetScoringTemplatesAsync(
        bool activeOnly,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        EnsureDatabaseConfigured(dbContext);

        var query = dbContext.ScoringTemplates
            .AsNoTracking()
            .Include(template => template.Rules)
            .AsQueryable();

        if (activeOnly)
        {
            query = query.Where(template => template.IsActive);
        }

        var templates = await query
            .OrderByDescending(template => template.IsActive)
            .ThenBy(template => template.TemplateKey)
            .ToListAsync(cancellationToken);

        return templates.Select(MapScoringTemplate).ToList();
    }

    private static YahooWeeklyPlayerStatResult MapWeeklyStat(WeeklyPlayerStat playerStat) =>
        new(
            playerStat.GameKey,
            playerStat.Season,
            playerStat.Week,
            playerStat.YahooPlayerId,
            playerStat.SleeperPlayerId,
            playerStat.FullName,
            playerStat.Team,
            playerStat.Position,
            playerStat.EditorialTeamAbbr,
            playerStat.UpdatedAtUtc,
            playerStat.StatValues
                .OrderBy(statValue => statValue.StatId)
                .Select(statValue => new YahooWeeklyPlayerStatValueResult(
                    statValue.StatId,
                    statValue.StatName,
                    statValue.Value))
                .ToList());

    private static YahooWeeklyPlayerPointResult MapWeeklyPoint(WeeklyPlayerPoint playerPoint) =>
        new(
            playerPoint.TemplateKey,
            playerPoint.WeeklyPlayerStat.Season,
            playerPoint.WeeklyPlayerStat.Week,
            playerPoint.WeeklyPlayerStat.YahooPlayerId,
            playerPoint.WeeklyPlayerStat.SleeperPlayerId,
            playerPoint.WeeklyPlayerStat.FullName,
            playerPoint.WeeklyPlayerStat.Team,
            playerPoint.WeeklyPlayerStat.Position,
            playerPoint.FantasyPoints,
            DeserializeBreakdown(playerPoint.BreakdownJson),
            playerPoint.CalculatedAtUtc);

    private static YahooPlayerSeasonPointsResult MapSeasonPoints(IReadOnlyList<WeeklyPlayerPoint> playerPoints)
    {
        var firstPoint = playerPoints[0];
        var totalFantasyPoints = playerPoints.Sum(point => point.FantasyPoints);

        return new YahooPlayerSeasonPointsResult(
            firstPoint.TemplateKey,
            firstPoint.WeeklyPlayerStat.Season,
            firstPoint.WeeklyPlayerStat.YahooPlayerId,
            firstPoint.WeeklyPlayerStat.SleeperPlayerId,
            firstPoint.WeeklyPlayerStat.FullName,
            firstPoint.WeeklyPlayerStat.Team,
            firstPoint.WeeklyPlayerStat.Position,
            playerPoints.Count,
            totalFantasyPoints,
            playerPoints.Count == 0 ? 0 : totalFantasyPoints / playerPoints.Count,
            playerPoints
                .Select(point => new YahooSeasonPointWeekResult(
                    point.WeeklyPlayerStat.Week,
                    point.FantasyPoints,
                    DeserializeBreakdown(point.BreakdownJson),
                    point.CalculatedAtUtc))
                .ToList());
    }

    private static ScoringTemplateResult MapScoringTemplate(ScoringTemplate template) =>
        new(
            template.TemplateKey,
            template.Name,
            template.Description,
            template.IsActive,
            template.UpdatedAtUtc,
            template.Rules
                .OrderBy(rule => rule.StatId)
                .Select(rule => new ScoringTemplateRuleResult(
                    rule.StatId,
                    rule.StatName,
                    rule.Modifier))
                .ToList());

    private static IReadOnlyDictionary<string, decimal>? DeserializeBreakdown(string? breakdownJson)
    {
        if (string.IsNullOrWhiteSpace(breakdownJson))
        {
            return null;
        }

        return JsonSerializer.Deserialize<Dictionary<string, decimal>>(breakdownJson, SerializerOptions);
    }

    private static string? NormalizePosition(string? position) =>
        string.IsNullOrWhiteSpace(position) ? null : position.Trim().ToUpperInvariant();

    private static int NormalizeLimit(int limit) => limit switch
    {
        <= 0 => 25,
        > 200 => 200,
        _ => limit
    };

    private static void EnsureDatabaseConfigured(LeagueApiDbContext dbContext)
    {
        if (string.IsNullOrWhiteSpace(dbContext.Database.ProviderName))
        {
            throw new InvalidOperationException(
                "Yahoo reads require ConnectionStrings:LeagueAPI to be configured and the database migrations to be applied.");
        }
    }

    private static async Task<string?> ResolveTemplateKeyAsync(
        LeagueApiDbContext dbContext,
        string? templateKey,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(templateKey))
        {
            var normalizedTemplateKey = templateKey.Trim();
            var exists = await dbContext.ScoringTemplates
                .AsNoTracking()
                .AnyAsync(template => template.TemplateKey == normalizedTemplateKey, cancellationToken);

            return exists ? normalizedTemplateKey : null;
        }

        return await dbContext.ScoringTemplates
            .AsNoTracking()
            .Where(template => template.IsActive)
            .OrderBy(template => template.TemplateKey)
            .Select(template => template.TemplateKey)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
