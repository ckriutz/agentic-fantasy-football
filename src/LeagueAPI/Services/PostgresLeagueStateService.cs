using LeagueAPI.Data;
using LeagueAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace LeagueAPI.Services;

public sealed class PostgresLeagueStateService(IDbContextFactory<LeagueApiDbContext> dbContextFactory) : ILeagueStateService
{
    private static readonly HashSet<string> ValidPhases =
    [
        LeagueStatePhases.GamesLocked,
        LeagueStatePhases.WaiverWindow,
        LeagueStatePhases.FreeAgency,
        LeagueStatePhases.Complete
    ];

    private static readonly HashSet<string> ValidUpdatedBy =
    [
        LeagueStateUpdatedBy.Manual,
        LeagueStateUpdatedBy.SeasonRunner,
        LeagueStateUpdatedBy.WaiverProcessor
    ];

    private readonly IDbContextFactory<LeagueApiDbContext> _dbContextFactory = dbContextFactory;

    public async Task<LeagueState> GetLeagueStateAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await GetOrCreateLeagueStateAsync(dbContext, cancellationToken);
        return MapToState(entity);
    }

    public async Task<LeagueState> SetLeagueStateAsync(int season, int week, string phase, string updatedBy, CancellationToken cancellationToken)
    {
        ValidateSeason(season);
        ValidateWeek(week);

        var normalizedPhase = NormalizePhase(phase);
        var normalizedUpdatedBy = NormalizeUpdatedBy(updatedBy);

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var state = await UpsertLeagueStateAsync(dbContext, season, week, normalizedPhase, normalizedUpdatedBy, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return state;
    }

    internal static async Task<LeagueState> UpsertLeagueStateAsync(LeagueApiDbContext dbContext, int season, int week, string phase, string updatedBy, CancellationToken cancellationToken)
    {
        ValidateSeason(season);
        ValidateWeek(week);

        var normalizedPhase = NormalizePhase(phase);
        var normalizedUpdatedBy = NormalizeUpdatedBy(updatedBy);
        var entity = await GetOrCreateLeagueStateAsync(dbContext, cancellationToken);

        entity.Season = season;
        entity.Week = week;
        entity.Phase = normalizedPhase;
        entity.UpdatedBy = normalizedUpdatedBy;
        entity.UpdatedAtUtc = DateTimeOffset.UtcNow;

        return MapToState(entity);
    }

    internal static LeagueState MapToState(LeagueStateEntity entity)
    {
        return new LeagueState(
            entity.Season,
            entity.Week,
            entity.Phase,
            entity.UpdatedAtUtc,
            entity.UpdatedBy);
    }

    private static void ValidateSeason(int season)
    {
        if (season <= 0)
            throw new ArgumentException("season must be a positive integer.", nameof(season));
    }

    private static void ValidateWeek(int week)
    {
        if (week is < 0 or > 17)
            throw new ArgumentException("week must be between 0 and 17.", nameof(week));
    }

    private static string NormalizePhase(string phase)
    {
        if (string.IsNullOrWhiteSpace(phase))
            throw new ArgumentException("phase is required.", nameof(phase));

        var normalizedPhase = phase.Trim();
        if (!ValidPhases.Contains(normalizedPhase))
            throw new ArgumentException("phase must be one of: games_locked, waiver_window, free_agency, complete.", nameof(phase));

        return normalizedPhase;
    }

    private static string NormalizeUpdatedBy(string updatedBy)
    {
        if (string.IsNullOrWhiteSpace(updatedBy))
            throw new ArgumentException("updatedBy is required.", nameof(updatedBy));

        var normalizedUpdatedBy = updatedBy.Trim();
        if (!ValidUpdatedBy.Contains(normalizedUpdatedBy))
            throw new ArgumentException("updatedBy must be one of: manual, season-runner, waiver-processor.", nameof(updatedBy));

        return normalizedUpdatedBy;
    }

    internal static async Task<LeagueStateEntity> GetOrCreateLeagueStateAsync(LeagueApiDbContext dbContext, CancellationToken cancellationToken)
    {
        var entity = await dbContext.LeagueState
            .FirstOrDefaultAsync(row => row.Id == LeagueStateDefaults.SingletonId, cancellationToken);

        if (entity is not null)
            return entity;

        entity = new LeagueStateEntity
        {
            Id = LeagueStateDefaults.SingletonId,
            Season = LeagueStateDefaults.DefaultSeason,
            Week = LeagueStateDefaults.PreseasonWeek,
            Phase = LeagueStateDefaults.DefaultPhase,
            UpdatedBy = LeagueStateDefaults.DefaultUpdatedBy,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        dbContext.LeagueState.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return entity;
    }
}
