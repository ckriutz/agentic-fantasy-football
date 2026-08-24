using LeagueAPI.Data;
using LeagueAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace LeagueAPI.Services;

public sealed class StageAwareFinalizationService(IDbContextFactory<LeagueApiDbContext> dbContextFactory, MatchupScoringService matchupScoringService, PlayoffService playoffService)
{
    private readonly IDbContextFactory<LeagueApiDbContext> _dbContextFactory = dbContextFactory;
    private readonly MatchupScoringService _matchupScoringService = matchupScoringService;
    private readonly PlayoffService _playoffService = playoffService;

    public async Task<StageAwareFinalizeResult> FinalizeWeekAsync(int season, int week, string updatedBy, CancellationToken cancellationToken)
    {
        var finalizedWeek = await _matchupScoringService.FinalizeWeekAsync(season, week, cancellationToken);

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var settings = await dbContext.PlayoffSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.Id == PlayoffSettingsDefaults.SingletonId, cancellationToken)
            ?? new PlayoffSettingsEntity();

        if (week != settings.RegularSeasonEndWeek || season != finalizedWeek.Season)
            return new StageAwareFinalizeResult(finalizedWeek, false, null);

        var leagueStateEntity = await dbContext.LeagueState.AsNoTracking().FirstOrDefaultAsync(row => row.Id == LeagueStateDefaults.SingletonId, cancellationToken);
        var currentSeasonStage = leagueStateEntity?.SeasonStage ?? LeagueStateDefaults.DefaultSeasonStage;

        // Only transition on the regular-season end week; once playoffs begin this endpoint just finalizes weeks.
        if (!string.Equals(currentSeasonStage, SeasonStages.RegularSeason, StringComparison.Ordinal))
            return new StageAwareFinalizeResult(finalizedWeek, false, null);

        var lockedBracket = await _playoffService.LockBracketAsync(season, updatedBy, cancellationToken);
        return new StageAwareFinalizeResult(finalizedWeek, true, lockedBracket);
    }
}
