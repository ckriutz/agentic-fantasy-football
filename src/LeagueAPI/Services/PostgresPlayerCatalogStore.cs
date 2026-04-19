using LeagueAPI.Data;
using LeagueAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace LeagueAPI.Services;

public sealed class PostgresPlayerCatalogStore(
    IDbContextFactory<LeagueApiDbContext> dbContextFactory,
    ILogger<PostgresPlayerCatalogStore> logger) : IPlayerCatalogReader, IPlayerCatalogPersistence
{
    private readonly IDbContextFactory<LeagueApiDbContext> _dbContextFactory = dbContextFactory;
    private readonly ILogger<PostgresPlayerCatalogStore> _logger = logger;

    public async Task<PlayerRecord?> GetBySleeperIdAsync(string sleeperPlayerId, CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var player = await dbContext.Players
            .AsNoTracking()
            .FirstOrDefaultAsync(
                entity => entity.SleeperPlayerId == sleeperPlayerId && entity.Active,
                cancellationToken);

        return player is null ? null : PlayerRecordFactory.Map(player);
    }

    public async Task<PlayerRecord?> GetByYahooIdAsync(int yahooId, CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var player = await dbContext.Players
            .AsNoTracking()
            .FirstOrDefaultAsync(
                entity => entity.YahooId == yahooId && entity.Active,
                cancellationToken);

        return player is null ? null : PlayerRecordFactory.Map(player);
    }

    public async Task<IReadOnlyList<PlayerRecord>> QueryAsync(PlayerQuery query, CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var playersQuery = PlayerCatalogQueryBuilder.ApplyFilters(
            dbContext.Players.AsNoTracking().Where(entity => entity.Active),
            query);
        var orderedQuery = PlayerCatalogQueryBuilder.ApplyOrdering(playersQuery, query);

        var players = await orderedQuery
            .ThenBy(entity => entity.SleeperPlayerId)
            .Take(PlayerCatalogQueryBuilder.NormalizeLimit(query.Limit))
            .ToListAsync(cancellationToken);

        return players.Select(PlayerRecordFactory.Map).ToArray();
    }

    public async Task RecordSyncStartedAsync(
        Guid syncRunId,
        DateTimeOffset startedAtUtc,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Sync started: {SyncRunId} at {StartedAtUtc}", syncRunId, startedAtUtc);

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        dbContext.SleeperSyncRuns.Add(new SleeperSyncRun
        {
            SyncRunId = syncRunId,
            StartedAtUtc = startedAtUtc,
            Status = "Started"
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task PersistPlayersAsync(
        IReadOnlyCollection<PlayerRecord> players,
        Guid syncRunId,
        DateTimeOffset persistedAtUtc,
        CancellationToken cancellationToken)
    {
        var filteredPlayers = players
            .Where(player => !PlayerRecordFactory.ShouldIgnore(player))
            .ToArray();

        var ignoredPlayerCount = players.Count - filteredPlayers.Length;
        if (ignoredPlayerCount > 0)
        {
            _logger.LogInformation(
                "Ignoring {IgnoredPlayerCount} Sleeper placeholder players before persistence (sync run: {SyncRunId})",
                ignoredPlayerCount,
                syncRunId);
        }

        _logger.LogInformation("Starting to persist {PlayerCount} players to database (sync run: {SyncRunId})", 
            filteredPlayers.Length, syncRunId);

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var sleeperPlayerIds = filteredPlayers.Select(player => player.SleeperPlayerId).ToArray();

        var existingPlayersById = await dbContext.Players
            .Where(entity => sleeperPlayerIds.Contains(entity.SleeperPlayerId))
            .ToDictionaryAsync(entity => entity.SleeperPlayerId, cancellationToken);

        var newPlayerCount = 0;
        var updatedPlayerCount = 0;

        foreach (var player in filteredPlayers)
        {
            if (!existingPlayersById.TryGetValue(player.SleeperPlayerId, out var entity))
            {
                entity = new PlayerEntity
                {
                    SleeperPlayerId = player.SleeperPlayerId,
                    SearchFullNameNormalized = string.Empty,
                    FantasyPositionsTokenized = string.Empty,
                    RawJson = string.Empty
                };
                dbContext.Players.Add(entity);
                newPlayerCount++;
            }
            else
            {
                updatedPlayerCount++;
            }

            entity.YahooId = player.YahooId;
            entity.FantasyDataId = player.FantasyDataId;
            entity.FullName = player.FullName;
            entity.FirstName = player.FirstName;
            entity.LastName = player.LastName;
            entity.SearchFullNameNormalized = player.SearchFullNameNormalized;
            entity.Team = player.Team;
            entity.TeamAbbr = player.TeamAbbr;
            entity.Position = player.Position;
            entity.FantasyPositionsTokenized = player.FantasyPositionsTokenized;
            entity.Status = player.Status;
            entity.Active = player.Active;
            entity.Sport = player.Sport;
            entity.RawJson = player.RawJson;
            entity.UpdatedAtUtc = persistedAtUtc;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Successfully persisted {TotalPlayers} players: {NewCount} new, {UpdatedCount} updated (sync run: {SyncRunId})", 
            filteredPlayers.Length, newPlayerCount, updatedPlayerCount, syncRunId);
    }

    public async Task RecordSyncCompletedAsync(SleeperSyncState syncState, CancellationToken cancellationToken)
    {
        var syncRunId = syncState.SyncRunId
            ?? throw new InvalidOperationException("A completed sync state must include a sync run ID.");

        _logger.LogInformation("Sync completed successfully: {SyncRunId}, {RecordCount} records",
            syncRunId, syncState.RecordCount);

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var syncRun = await dbContext.SleeperSyncRuns
            .FirstOrDefaultAsync(entity => entity.SyncRunId == syncRunId, cancellationToken)
            ?? throw new InvalidOperationException($"Sync run {syncRunId} was not found.");

        syncRun.CompletedAtUtc = syncState.LastSuccessfulSyncAtUtc ?? DateTimeOffset.UtcNow;
        syncRun.Status = syncState.Status;
        syncRun.RecordCount = syncState.RecordCount;
        syncRun.ErrorMessage = syncState.ErrorMessage;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RecordSyncFailedAsync(
        Guid syncRunId,
        DateTimeOffset failedAtUtc,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        _logger.LogError("Sync failed: {SyncRunId} - Error: {ErrorMessage}", syncRunId, errorMessage);

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var syncRun = await dbContext.SleeperSyncRuns
            .FirstOrDefaultAsync(entity => entity.SyncRunId == syncRunId, cancellationToken)
            ?? throw new InvalidOperationException($"Sync run {syncRunId} was not found.");

        syncRun.CompletedAtUtc = failedAtUtc;
        syncRun.Status = "Failed";
        syncRun.ErrorMessage = errorMessage;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<SleeperSyncState> GetLatestSyncStateAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var latestRun = await dbContext.SleeperSyncRuns
            .AsNoTracking()
            .OrderByDescending(run => run.StartedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (latestRun is null)
        {
            return new SleeperSyncState();
        }

        var latestSuccessfulAtUtc = await dbContext.SleeperSyncRuns
            .AsNoTracking()
            .Where(run => run.Status == "Succeeded")
            .OrderByDescending(run => run.CompletedAtUtc)
            .Select(run => (DateTimeOffset?)run.CompletedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        return new SleeperSyncState
        {
            SyncRunId = latestRun.SyncRunId,
            Status = latestRun.Status,
            LastAttemptedAtUtc = latestRun.StartedAtUtc,
            LastSuccessfulSyncAtUtc = latestSuccessfulAtUtc,
            RecordCount = latestRun.RecordCount,
            ErrorMessage = latestRun.ErrorMessage
        };
    }

}
