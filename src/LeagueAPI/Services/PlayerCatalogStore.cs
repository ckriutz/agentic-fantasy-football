using LeagueAPI.Data;
using LeagueAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace LeagueAPI.Services;

public sealed class PlayerCatalogStore(
    IDbContextFactory<LeagueApiDbContext> dbContextFactory,
    ILogger<PlayerCatalogStore> logger) : IPlayerCatalogReader, IPlayerCatalogPersistence
{
    private readonly IDbContextFactory<LeagueApiDbContext> _dbContextFactory = dbContextFactory;
    private readonly ILogger<PlayerCatalogStore> _logger = logger;

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

    public async Task<IReadOnlyList<PlayerRecord>> QueryAsync(PlayerQuery query, CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var normalizedLimit = PlayerCatalogQueryBuilder.NormalizeLimit(query.Limit);
        var playersQuery = PlayerCatalogQueryBuilder.ApplyFilters(
            dbContext.Players.AsNoTracking().Where(entity => entity.Active),
            query);

        if (string.IsNullOrWhiteSpace(query.SortBy))
        {
            var matchedPlayers = await playersQuery.ToListAsync(cancellationToken);

            return matchedPlayers
                .Select(PlayerRecordFactory.Map)
                .OrderBy(player => player.SearchRank ?? int.MaxValue)
                .ThenBy(player => player.FullName ?? player.SleeperPlayerId)
                .ThenBy(player => player.SleeperPlayerId)
                .Take(normalizedLimit)
                .ToArray();
        }

        var orderedQuery = PlayerCatalogQueryBuilder.ApplyOrdering(playersQuery, query);

        var players = await orderedQuery
            .ThenBy(entity => entity.SleeperPlayerId)
            .Take(normalizedLimit)
            .ToListAsync(cancellationToken);

        return players.Select(PlayerRecordFactory.Map).ToArray();
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
            entity.SportradarId = player.SportradarId;
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
            entity.Sport = player.Data.Sport;
            entity.RawJson = player.RawJson;
            entity.UpdatedAtUtc = persistedAtUtc;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Successfully persisted {TotalPlayers} players: {NewCount} new, {UpdatedCount} updated (sync run: {SyncRunId})", 
            filteredPlayers.Length, newPlayerCount, updatedPlayerCount, syncRunId);
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
