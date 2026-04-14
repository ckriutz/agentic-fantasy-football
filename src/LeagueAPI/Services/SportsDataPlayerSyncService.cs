using System.Text.Json;
using LeagueAPI.Data;
using LeagueAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace LeagueAPI.Services;

public sealed class SportsDataPlayerSyncService(
    SportsDataApiClient sportsDataApiClient,
    IDbContextFactory<LeagueApiDbContext> dbContextFactory,
    ILogger<SportsDataPlayerSyncService> logger)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly SportsDataApiClient _sportsDataApiClient = sportsDataApiClient;
    private readonly IDbContextFactory<LeagueApiDbContext> _dbContextFactory = dbContextFactory;
    private readonly ILogger<SportsDataPlayerSyncService> _logger = logger;
    private readonly SemaphoreSlim _syncLock = new(1, 1);

    public async Task<SportsDataSyncRun> SyncPlayersAsync(bool force, CancellationToken cancellationToken)
    {
        await _syncLock.WaitAsync(cancellationToken);

        try
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

            var nowUtc = DateTimeOffset.UtcNow;
            var latestRun = await GetLatestRunAsync(dbContext, cancellationToken);

            if (!force
                && latestRun?.Status == "Succeeded"
                && latestRun.CompletedAtUtc?.UtcDateTime.Date == nowUtc.UtcDateTime.Date)
            {
                return new SportsDataSyncRun
                {
                    SyncRunId = latestRun.SyncRunId,
                    StartedAtUtc = latestRun.StartedAtUtc,
                    CompletedAtUtc = latestRun.CompletedAtUtc,
                    Status = "Skipped",
                    RecordCount = latestRun.RecordCount,
                    ErrorMessage = latestRun.ErrorMessage
                };
            }

            var syncRun = new SportsDataSyncRun
            {
                SyncRunId = Guid.NewGuid(),
                StartedAtUtc = nowUtc,
                Status = "Started"
            };

            dbContext.SportsDataSyncRuns.Add(syncRun);
            await dbContext.SaveChangesAsync(cancellationToken);

            try
            {
                var payload = await _sportsDataApiClient.GetFantasyPlayersJsonAsync(cancellationToken);
                var sportsDataPlayers =
                    JsonSerializer.Deserialize<List<SportsDataFantasyPlayer>>(payload, SerializerOptions)
                    ?? throw new InvalidOperationException("SportsData returned an invalid fantasy players response.");

                await UpsertSportsDataPlayersAsync(dbContext, sportsDataPlayers, nowUtc, cancellationToken);
                var (linkedCount, unlinkedCount) =
                    await ApplySportsDataEnrichmentAsync(dbContext, sportsDataPlayers, cancellationToken);

                syncRun.CompletedAtUtc = nowUtc;
                syncRun.Status = "Succeeded";
                syncRun.RecordCount = sportsDataPlayers.Count;
                syncRun.ErrorMessage = null;

                await dbContext.SaveChangesAsync(cancellationToken);

                _logger.LogInformation(
                    "SportsData sync completed: {SyncRunId}, fetched {FetchedCount} rows, linked {LinkedCount} Sleeper players, unlinked {UnlinkedCount}.",
                    syncRun.SyncRunId,
                    sportsDataPlayers.Count,
                    linkedCount,
                    unlinkedCount);

                return syncRun;
            }
            catch (Exception exception)
            {
                syncRun.CompletedAtUtc = DateTimeOffset.UtcNow;
                syncRun.Status = "Failed";
                syncRun.ErrorMessage = exception.Message;

                await dbContext.SaveChangesAsync(cancellationToken);

                _logger.LogError(exception, "SportsData sync failed for sync run {SyncRunId}.", syncRun.SyncRunId);
                throw;
            }
        }
        finally
        {
            _syncLock.Release();
        }
    }

    public async Task<SportsDataSyncRun?> GetLatestSyncRunAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await GetLatestRunAsync(dbContext, cancellationToken);
    }

    private async Task<SportsDataSyncRun?> GetLatestRunAsync(
        LeagueApiDbContext dbContext,
        CancellationToken cancellationToken)
    {
        return await dbContext.SportsDataSyncRuns
            .AsNoTracking()
            .OrderByDescending(run => run.StartedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static async Task UpsertSportsDataPlayersAsync(
        LeagueApiDbContext dbContext,
        IReadOnlyCollection<SportsDataFantasyPlayer> sportsDataPlayers,
        DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken)
    {
        var playerIds = sportsDataPlayers.Select(player => player.PlayerID).ToArray();
        var existingPlayersById = await dbContext.SportsDataFantasyPlayers
            .Where(entity => playerIds.Contains(entity.SportsDataPlayerId))
            .ToDictionaryAsync(entity => entity.SportsDataPlayerId, cancellationToken);

        foreach (var sportsDataPlayer in sportsDataPlayers)
        {
            if (!existingPlayersById.TryGetValue(sportsDataPlayer.PlayerID, out var entity))
            {
                entity = new SportsDataFantasyPlayerEntity
                {
                    SportsDataPlayerId = sportsDataPlayer.PlayerID,
                    RawJson = string.Empty
                };
                dbContext.SportsDataFantasyPlayers.Add(entity);
            }

            entity.Name = sportsDataPlayer.Name;
            entity.Team = sportsDataPlayer.Team;
            entity.Position = sportsDataPlayer.Position;
            entity.FantasyPlayerKey = sportsDataPlayer.FantasyPlayerKey;
            entity.AverageDraftPosition = sportsDataPlayer.AverageDraftPosition;
            entity.AverageDraftPositionPPR = sportsDataPlayer.AverageDraftPositionPPR;
            entity.ByeWeek = sportsDataPlayer.ByeWeek;
            entity.LastSeasonFantasyPoints = sportsDataPlayer.LastSeasonFantasyPoints;
            entity.ProjectedFantasyPoints = sportsDataPlayer.ProjectedFantasyPoints;
            entity.AuctionValue = sportsDataPlayer.AuctionValue;
            entity.AuctionValuePPR = sportsDataPlayer.AuctionValuePPR;
            entity.AverageDraftPositionIDP = sportsDataPlayer.AverageDraftPositionIDP;
            entity.AverageDraftPositionRookie = sportsDataPlayer.AverageDraftPositionRookie;
            entity.AverageDraftPositionDynasty = sportsDataPlayer.AverageDraftPositionDynasty;
            entity.AverageDraftPosition2QB = sportsDataPlayer.AverageDraftPosition2QB;
            entity.RawJson = JsonSerializer.Serialize(sportsDataPlayer, SerializerOptions);
            entity.UpdatedAtUtc = updatedAtUtc;
        }
    }

    private static async Task<(int linkedCount, int unlinkedCount)> ApplySportsDataEnrichmentAsync(
        LeagueApiDbContext dbContext,
        IReadOnlyCollection<SportsDataFantasyPlayer> sportsDataPlayers,
        CancellationToken cancellationToken)
    {
        var sportsDataPlayersById = sportsDataPlayers.ToDictionary(player => player.PlayerID);
        var sleeperPlayers = await dbContext.Players
            .Where(player => player.FantasyDataId != null)
            .ToListAsync(cancellationToken);

        var linkedCount = 0;
        var unlinkedCount = 0;

        foreach (var sleeperPlayer in sleeperPlayers)
        {
            if (sleeperPlayer.FantasyDataId is int fantasyDataId
                && sportsDataPlayersById.TryGetValue(fantasyDataId, out var sportsDataPlayer))
            {
                sleeperPlayer.AverageDraftPosition = sportsDataPlayer.AverageDraftPosition;
                sleeperPlayer.ByeWeek = sportsDataPlayer.ByeWeek;
                sleeperPlayer.LastSeasonFantasyPoints = sportsDataPlayer.LastSeasonFantasyPoints;
                sleeperPlayer.ProjectedFantasyPoints = sportsDataPlayer.ProjectedFantasyPoints;
                sleeperPlayer.AuctionValue = sportsDataPlayer.AuctionValue;
                linkedCount++;
            }
            else
            {
                sleeperPlayer.AverageDraftPosition = null;
                sleeperPlayer.ByeWeek = null;
                sleeperPlayer.LastSeasonFantasyPoints = null;
                sleeperPlayer.ProjectedFantasyPoints = null;
                sleeperPlayer.AuctionValue = null;
                unlinkedCount++;
            }
        }

        return (linkedCount, unlinkedCount);
    }
}
