using LeagueAPI.Data;
using LeagueAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace LeagueAPI.Services;

public sealed class PlayerGameLockService(IDbContextFactory<LeagueApiDbContext> dbContextFactory, LeagueStateService leagueStateService)
{
    private readonly IDbContextFactory<LeagueApiDbContext> _dbContextFactory = dbContextFactory;
    private readonly LeagueStateService _leagueStateService = leagueStateService;

    public async Task<IReadOnlyDictionary<string, PlayerLockStatus>> GetPlayerLockStatusesAsync(IReadOnlyCollection<string> sleeperPlayerIds, CancellationToken cancellationToken)
    {
        var normalizedSleeperPlayerIds = sleeperPlayerIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (normalizedSleeperPlayerIds.Length == 0)
            return new Dictionary<string, PlayerLockStatus>(StringComparer.Ordinal);

        var leagueState = await _leagueStateService.GetLeagueStateAsync(cancellationToken);
        var isAddDropLockedByPhase = string.Equals(leagueState.Phase, LeagueStatePhases.GamesLocked, StringComparison.Ordinal);

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        // Row existence on weekly_player_scores is the has-played signal. Players whose FantasyPros
        // rows never resolved a SleeperPlayerId cannot lock via this path (null ids are excluded).
        var playedSleeperPlayerIds = await dbContext.WeeklyPlayerScores
            .AsNoTracking()
            .Where(score =>
                score.Season == leagueState.Season
                && score.Week == leagueState.Week
                && score.SleeperPlayerId != null
                && normalizedSleeperPlayerIds.Contains(score.SleeperPlayerId))
            .Select(score => score.SleeperPlayerId!)
            .Distinct()
            .ToListAsync(cancellationToken);

        var playedSleeperPlayerIdSet = new HashSet<string>(playedSleeperPlayerIds, StringComparer.Ordinal);

        return normalizedSleeperPlayerIds.ToDictionary(
            sleeperPlayerId => sleeperPlayerId,
            sleeperPlayerId => CreateLockStatus(playedSleeperPlayerIdSet.Contains(sleeperPlayerId), isAddDropLockedByPhase),
            StringComparer.Ordinal);
    }

    private static PlayerLockStatus CreateLockStatus(bool hasPlayedThisWeek, bool isAddDropLockedByPhase)
    {
        var isAddDropLocked = isAddDropLockedByPhase || hasPlayedThisWeek;
        var addDropLockReason = isAddDropLockedByPhase
            ? "Add/drop moves are locked because the current league phase is games_locked."
            : hasPlayedThisWeek
                ? "Player has already played this week and cannot be added or dropped."
                : null;

        var isLineupMoveLocked = hasPlayedThisWeek;
        var lineupMoveLockReason = hasPlayedThisWeek
            ? "Player has already played this week and cannot move between the bench and a starter slot."
            : null;

        return new PlayerLockStatus(
            hasPlayedThisWeek,
            isAddDropLocked,
            addDropLockReason,
            isLineupMoveLocked,
            lineupMoveLockReason);
    }
}
