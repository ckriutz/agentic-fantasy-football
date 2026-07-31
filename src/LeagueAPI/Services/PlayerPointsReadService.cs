using LeagueAPI.Data;
using LeagueAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace LeagueAPI.Services;

public sealed class PlayerPointsReadService(IDbContextFactory<LeagueApiDbContext> dbContextFactory)
{
    private readonly IDbContextFactory<LeagueApiDbContext> _dbContextFactory = dbContextFactory;

    public async Task<IReadOnlyList<WeeklyPlayerScoreResult>> GetWeeklyPointsAsync(int season, int week, string? position, int limit, CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        EnsureDatabaseConfigured(dbContext);

        var normalizedPosition = NormalizePosition(position);
        var normalizedLimit = NormalizeLimit(limit);

        var query = dbContext.WeeklyPlayerScores
            .AsNoTracking()
            .Where(score => score.Season == season && score.Week == week && score.SleeperPlayerId != null);

        if (normalizedPosition is not null)
        {
            query = query.Where(score => score.PositionId == normalizedPosition);
        }

        // Deduplicate by SleeperPlayerId: if multiple FantasyPros rows map to one Sleeper id,
        // keep the lowest FantasyProsPlayerId so each player appears once.
        var scores = await query
            .OrderByDescending(score => score.Points)
            .ThenBy(score => score.PlayerName)
            .ThenBy(score => score.FantasyProsPlayerId)
            .ToListAsync(cancellationToken);

        return DeduplicateBySleeperPlayerId(scores)
            .Take(normalizedLimit)
            .Select(MapWeeklyScore)
            .ToList();
    }

    public async Task<WeeklyPlayerScoreResult?> GetPlayerWeeklyPointsAsync(string sleeperPlayerId, int season, int week, CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        EnsureDatabaseConfigured(dbContext);

        var normalizedSleeperPlayerId = NormalizeSleeperPlayerId(sleeperPlayerId);
        if (normalizedSleeperPlayerId is null)
        {
            return null;
        }

        var scores = await dbContext.WeeklyPlayerScores
            .AsNoTracking()
            .Where(score =>
                score.Season == season
                && score.Week == week
                && score.SleeperPlayerId == normalizedSleeperPlayerId)
            .OrderBy(score => score.FantasyProsPlayerId)
            .ToListAsync(cancellationToken);

        var score = scores.FirstOrDefault();
        return score is null ? null : MapWeeklyScore(score);
    }

    public async Task<PlayerSeasonPointsResult?> GetPlayerSeasonPointsAsync(string sleeperPlayerId, int season, CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        EnsureDatabaseConfigured(dbContext);

        var normalizedSleeperPlayerId = NormalizeSleeperPlayerId(sleeperPlayerId);
        if (normalizedSleeperPlayerId is null)
        {
            return null;
        }

        var scores = await dbContext.WeeklyPlayerScores
            .AsNoTracking()
            .Where(score => score.Season == season && score.SleeperPlayerId == normalizedSleeperPlayerId)
            .OrderBy(score => score.Week)
            .ThenBy(score => score.FantasyProsPlayerId)
            .ToListAsync(cancellationToken);

        if (scores.Count == 0)
        {
            return null;
        }

        // One row per week: if duplicates exist for a week, keep the lowest FantasyProsPlayerId.
        var weeklyScores = scores
            .GroupBy(score => score.Week)
            .Select(group => group.OrderBy(score => score.FantasyProsPlayerId).First())
            .OrderBy(score => score.Week)
            .ToList();

        var first = weeklyScores[0];
        var totalPoints = weeklyScores.Sum(score => score.Points);
        var gamesCount = weeklyScores.Count;

        return new PlayerSeasonPointsResult(
            season,
            normalizedSleeperPlayerId,
            first.PlayerName,
            first.PositionId,
            first.TeamId,
            gamesCount,
            totalPoints,
            gamesCount == 0 ? 0m : totalPoints / gamesCount,
            weeklyScores
                .Select(score => new SeasonPointWeekResult(score.Week, score.Points, score.UpdatedAtUtc))
                .ToList());
    }

    private static IEnumerable<WeeklyPlayerScoreEntity> DeduplicateBySleeperPlayerId(IEnumerable<WeeklyPlayerScoreEntity> scores)
    {
        // Input is expected pre-ordered by points desc; within a Sleeper id keep lowest FantasyProsPlayerId.
        return scores
            .GroupBy(score => score.SleeperPlayerId!, StringComparer.Ordinal)
            .Select(group => group.OrderBy(score => score.FantasyProsPlayerId).First())
            .OrderByDescending(score => score.Points)
            .ThenBy(score => score.PlayerName)
            .ThenBy(score => score.FantasyProsPlayerId);
    }

    private static WeeklyPlayerScoreResult MapWeeklyScore(WeeklyPlayerScoreEntity score) =>
        new(
            score.Season,
            score.Week,
            score.FantasyProsPlayerId,
            score.SleeperPlayerId,
            score.PlayerName,
            score.PositionId,
            score.TeamId,
            score.Points,
            score.UpdatedAtUtc);

    private static string? NormalizePosition(string? position) =>
        string.IsNullOrWhiteSpace(position) ? null : position.Trim().ToUpperInvariant();

    private static string? NormalizeSleeperPlayerId(string? sleeperPlayerId) =>
        string.IsNullOrWhiteSpace(sleeperPlayerId) ? null : sleeperPlayerId.Trim();

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
                "Points reads require DBConnectionString to be configured and the database migrations to be applied.");
        }
    }
}
