using LeagueAPI.Data;
using LeagueAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace LeagueAPI.Services;

public sealed class MatchupScoringService(IDbContextFactory<LeagueApiDbContext> dbContextFactory)
{
    private const long FinalizationLockKey = 55002;

    private readonly IDbContextFactory<LeagueApiDbContext> _dbContextFactory = dbContextFactory;

    public async Task<MatchupScoreUpdateResult> UpdateLiveScoresAsync(int season, int week, CancellationToken cancellationToken)
    {
        ValidateSeasonAndWeek(season, week);

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var matchups = await dbContext.Matchups
            .Where(matchup => matchup.Week == week && !matchup.IsComplete)
            .ToListAsync(cancellationToken);

        var scoresByAgentId = await LoadCurrentStarterScoresAsync(dbContext, season, week, matchups, cancellationToken);
        UpdateMatchups(matchups, scoresByAgentId, finalize: false);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new MatchupScoreUpdateResult(season, week, matchups.Count, false, DateTimeOffset.UtcNow);
    }

    public async Task<MatchupScoreUpdateResult> FinalizeWeekAsync(int season, int week, CancellationToken cancellationToken)
    {
        ValidateSeasonAndWeek(season, week);

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({FinalizationLockKey})",
            cancellationToken);

        var matchups = await dbContext.Matchups
            .Where(matchup => matchup.Week == week)
            .ToListAsync(cancellationToken);

        if (matchups.Count == 0)
        {
            throw new InvalidOperationException($"No matchups exist for week {week}.");
        }

        var finalizedAtUtc = DateTimeOffset.UtcNow;
        var hasSnapshots = await dbContext.WeeklyRosterSnapshots
            .AnyAsync(snapshot => snapshot.Season == season && snapshot.Week == week, cancellationToken);

        if (!hasSnapshots)
        {
            var participatingAgentIds = matchups
                .SelectMany(matchup => new[] { matchup.HomeAgentId, matchup.AwayAgentId })
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            var assignments = await dbContext.RosterAssignments
                .Where(assignment => participatingAgentIds.Contains(assignment.AgentId))
                .ToListAsync(cancellationToken);

            dbContext.WeeklyRosterSnapshots.AddRange(assignments.Select(assignment => new WeeklyRosterSnapshot
            {
                Season = season,
                Week = week,
                AgentId = assignment.AgentId,
                SleeperPlayerId = assignment.SleeperPlayerId,
                SlotType = RosterSlotRules.NormalizeSlotType(assignment.SlotType),
                IsStarter = RosterSlotRules.IsStarterSlot(assignment.SlotType),
                FinalizedAtUtc = finalizedAtUtc
            }));

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var scoresByAgentId = await LoadSnapshotStarterScoresAsync(dbContext, season, week, matchups, cancellationToken);
        UpdateMatchups(matchups, scoresByAgentId, finalize: true);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new MatchupScoreUpdateResult(season, week, matchups.Count, true, finalizedAtUtc);
    }

    private static async Task<Dictionary<string, decimal>> LoadCurrentStarterScoresAsync(
        LeagueApiDbContext dbContext,
        int season,
        int week,
        IReadOnlyCollection<MatchupEntity> matchups,
        CancellationToken cancellationToken)
    {
        var participatingAgentIds = matchups
            .SelectMany(matchup => new[] { matchup.HomeAgentId, matchup.AwayAgentId })
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var starters = await dbContext.RosterAssignments
            .AsNoTracking()
            .Where(assignment => participatingAgentIds.Contains(assignment.AgentId))
            .ToListAsync(cancellationToken);

        return await LoadScoresByAgentIdAsync(
            dbContext,
            season,
            week,
            starters
                .Where(assignment => RosterSlotRules.IsStarterSlot(assignment.SlotType))
                .Select(assignment => new RosterScoreEntry(assignment.AgentId, assignment.SleeperPlayerId))
                .ToArray(),
            cancellationToken);
    }

    private static async Task<Dictionary<string, decimal>> LoadSnapshotStarterScoresAsync(
        LeagueApiDbContext dbContext,
        int season,
        int week,
        IReadOnlyCollection<MatchupEntity> matchups,
        CancellationToken cancellationToken)
    {
        var participatingAgentIds = matchups
            .SelectMany(matchup => new[] { matchup.HomeAgentId, matchup.AwayAgentId })
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var starters = await dbContext.WeeklyRosterSnapshots
            .AsNoTracking()
            .Where(snapshot =>
                snapshot.Season == season
                && snapshot.Week == week
                && participatingAgentIds.Contains(snapshot.AgentId)
                && snapshot.IsStarter)
            .Select(snapshot => new RosterScoreEntry(snapshot.AgentId, snapshot.SleeperPlayerId))
            .ToListAsync(cancellationToken);

        return await LoadScoresByAgentIdAsync(dbContext, season, week, starters, cancellationToken);
    }

    private static async Task<Dictionary<string, decimal>> LoadScoresByAgentIdAsync(
        LeagueApiDbContext dbContext,
        int season,
        int week,
        IReadOnlyCollection<RosterScoreEntry> starters,
        CancellationToken cancellationToken)
    {
        if (starters.Count == 0)
        {
            return new Dictionary<string, decimal>(StringComparer.Ordinal);
        }

        var activeTemplateKey = await dbContext.ScoringTemplates
            .AsNoTracking()
            .Where(template => template.IsActive)
            .OrderBy(template => template.TemplateKey)
            .Select(template => template.TemplateKey)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("No active scoring template is configured.");

        var playerIds = starters
            .Select(starter => starter.SleeperPlayerId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var pointsByPlayerId = await (
            from point in dbContext.WeeklyPlayerPoints.AsNoTracking()
            join stat in dbContext.WeeklyPlayerStats.AsNoTracking()
                on point.WeeklyPlayerStatId equals stat.WeeklyPlayerStatId
            where point.TemplateKey == activeTemplateKey
                && stat.Season == season
                && stat.Week == week
                && stat.SleeperPlayerId != null
                && playerIds.Contains(stat.SleeperPlayerId)
            select new { SleeperPlayerId = stat.SleeperPlayerId!, point.FantasyPoints })
            .ToListAsync(cancellationToken);

        var pointsBySleeperPlayerId = pointsByPlayerId
            .GroupBy(point => point.SleeperPlayerId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Sum(point => point.FantasyPoints), StringComparer.Ordinal);

        return starters
            .GroupBy(starter => starter.AgentId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(starter => pointsBySleeperPlayerId.GetValueOrDefault(starter.SleeperPlayerId)),
                StringComparer.Ordinal);
    }

    private static void UpdateMatchups(
        IReadOnlyCollection<MatchupEntity> matchups,
        IReadOnlyDictionary<string, decimal> scoresByAgentId,
        bool finalize)
    {
        foreach (var matchup in matchups)
        {
            matchup.HomePoints = scoresByAgentId.GetValueOrDefault(matchup.HomeAgentId);
            matchup.AwayPoints = scoresByAgentId.GetValueOrDefault(matchup.AwayAgentId);

            if (!finalize)
            {
                continue;
            }

            matchup.IsComplete = true;
            matchup.IsTie = matchup.HomePoints == matchup.AwayPoints;
            matchup.WinnerAgentId = matchup.IsTie
                ? null
                : matchup.HomePoints > matchup.AwayPoints
                    ? matchup.HomeAgentId
                    : matchup.AwayAgentId;
        }
    }

    private static void ValidateSeasonAndWeek(int season, int week)
    {
        if (season <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(season), "season must be greater than zero.");
        }

        if (week is < 1 or > 17)
        {
            throw new ArgumentOutOfRangeException(nameof(week), "week must be between 1 and 17.");
        }
    }

    private sealed record RosterScoreEntry(string AgentId, string SleeperPlayerId);
}
