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

        if (week == settings.RegularSeasonEndWeek && season == finalizedWeek.Season)
        {
            var leagueStateEntity = await dbContext.LeagueState.AsNoTracking().FirstOrDefaultAsync(row => row.Id == LeagueStateDefaults.SingletonId, cancellationToken);
            var currentSeasonStage = leagueStateEntity?.SeasonStage ?? LeagueStateDefaults.DefaultSeasonStage;

            // Only lock on the regular-season end week; playoff weeks resolve/advance below.
            if (string.Equals(currentSeasonStage, SeasonStages.RegularSeason, StringComparison.Ordinal))
            {
                var lockedBracket = await _playoffService.LockBracketAsync(season, updatedBy, cancellationToken);
                return new StageAwareFinalizeResult(finalizedWeek, true, lockedBracket, false, null, false);
            }

            return new StageAwareFinalizeResult(finalizedWeek, false, null, false, null, false);
        }

        if (season == finalizedWeek.Season && IsPlayoffWeek(settings, week))
        {
            var resolution = await _playoffService.ResolveRoundAsync(season, week, updatedBy, cancellationToken);
            return new StageAwareFinalizeResult(finalizedWeek, false, null, resolution.Advanced, resolution, resolution.SeasonCompleted);
        }

        return new StageAwareFinalizeResult(finalizedWeek, false, null, false, null, false);
    }

    private static bool IsPlayoffWeek(PlayoffSettingsEntity settings, int week) => week == settings.PlayoffStartWeek || week == settings.PlayoffStartWeek + 1 || week == settings.ChampionshipWeek;
}
