using ModelContextProtocol.Server;
using LeagueAPI.Models;
using LeagueAPI.Services;
using System.ComponentModel;

namespace LeagueAPI.Tools;

[McpServerToolType]
public sealed class YahooReadTools(
    YahooReadService yahooReadService,
    YahooPlayerSyncService yahooPlayerSyncService)
{
    private readonly YahooReadService _yahooReadService = yahooReadService;
    private readonly YahooPlayerSyncService _yahooPlayerSyncService = yahooPlayerSyncService;

    [McpServerTool, Description("Get a player's Yahoo weekly raw stats by Yahoo player ID.")]
    public Task<YahooWeeklyPlayerStatResult?> GetPlayerWeeklyStats(
        [Description("The Yahoo player ID.")] int yahooId,
        [Description("The season year, such as 2024.")] int season,
        [Description("The NFL week number.")] int week)
    {
        return _yahooReadService.GetPlayerWeeklyStatsByYahooIdAsync(
            yahooId,
            season,
            week,
            CancellationToken.None);
    }

    [McpServerTool, Description("Get a player's Yahoo weekly fantasy points by Yahoo player ID.")]
    public Task<YahooWeeklyPlayerPointResult?> GetPlayerWeeklyPoints(
        [Description("The Yahoo player ID.")] int yahooId,
        [Description("The season year, such as 2024.")] int season,
        [Description("The NFL week number.")] int week,
        [Description("Optional scoring template key. Uses the first active template when omitted.")] string? templateKey = null)
    {
        return _yahooReadService.GetPlayerWeeklyPointsByYahooIdAsync(
            yahooId,
            season,
            week,
            templateKey,
            CancellationToken.None);
    }

    [McpServerTool, Description("Get the top Yahoo weekly scorers for a season/week.")]
    public Task<IReadOnlyList<YahooWeeklyPlayerPointResult>> GetTopScorersByWeek(
        [Description("The season year, such as 2024.")] int season,
        [Description("The NFL week number.")] int week,
        [Description("Optional scoring template key. Uses the first active template when omitted.")] string? templateKey = null,
        [Description("Optional position filter such as QB, RB, WR, or TE.")] string? position = null,
        [Description("Maximum number of players to return.")] int limit = 25)
    {
        return _yahooReadService.GetWeeklyPointsAsync(
            season,
            week,
            templateKey,
            position,
            limit,
            CancellationToken.None);
    }

    [McpServerTool, Description("Get a player's Yahoo season fantasy point totals by Yahoo player ID.")]
    public Task<YahooPlayerSeasonPointsResult?> GetPlayerSeasonPoints(
        [Description("The Yahoo player ID.")] int yahooId,
        [Description("The season year, such as 2024.")] int season,
        [Description("Optional scoring template key. Uses the first active template when omitted.")] string? templateKey = null)
    {
        return _yahooReadService.GetPlayerSeasonPointsByYahooIdAsync(
            yahooId,
            season,
            templateKey,
            CancellationToken.None);
    }

    [McpServerTool, Description("Get the configured Yahoo scoring templates and their stat modifiers.")]
    public Task<IReadOnlyList<ScoringTemplateResult>> GetScoringTemplates(
        [Description("When true, only returns active templates.")] bool activeOnly = true)
    {
        return _yahooReadService.GetScoringTemplatesAsync(activeOnly, CancellationToken.None);
    }

    [McpServerTool, Description("Get the latest Yahoo weekly sync status.")]
    public Task<YahooSyncRun?> GetLatestYahooSyncStatus(
        [Description("Optional Yahoo game key filter.")] string? gameKey = null,
        [Description("Optional season filter.")] int? season = null,
        [Description("Optional week filter.")] int? week = null)
    {
        return _yahooPlayerSyncService.GetLatestSyncRunAsync(gameKey, season, week, CancellationToken.None);
    }
}
