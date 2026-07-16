using LeagueAPI.Data;
using LeagueAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace LeagueAPI.Services;

public sealed class PlayerGameLockService(IDbContextFactory<LeagueApiDbContext> dbContextFactory, LeagueStateService leagueStateService)
{
    private const int GamesPlayedStatId = 0;

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

        var playedSleeperPlayerIds = await dbContext.WeeklyPlayerStatValues
            .AsNoTracking()
            .Where(statValue =>
                statValue.StatId == GamesPlayedStatId
                && statValue.Value > 0
                && statValue.WeeklyPlayerStat.Season == leagueState.Season
                && statValue.WeeklyPlayerStat.Week == leagueState.Week
                && statValue.WeeklyPlayerStat.SleeperPlayerId != null
                && normalizedSleeperPlayerIds.Contains(statValue.WeeklyPlayerStat.SleeperPlayerId))
            .Select(statValue => statValue.WeeklyPlayerStat.SleeperPlayerId!)
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
