using System.Text.Json;
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

        return player is null ? null : MapPlayer(player);
    }

    public async Task<PlayerRecord?> GetByYahooIdAsync(int yahooId, CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var player = await dbContext.Players
            .AsNoTracking()
            .FirstOrDefaultAsync(
                entity => entity.YahooId == yahooId && entity.Active,
                cancellationToken);

        return player is null ? null : MapPlayer(player);
    }

    public async Task<IReadOnlyList<PlayerRecord>> QueryAsync(PlayerQuery query, CancellationToken cancellationToken)
    {
        var normalizedName = PlayerRecordFactory.NormalizeName(query.Name);
        var normalizedTeam = PlayerRecordFactory.NormalizeToken(query.Team);
        var normalizedPosition = PlayerRecordFactory.NormalizeToken(query.Position);
        var limit = Math.Clamp(query.Limit, 1, 200);

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var playersQuery = dbContext.Players.AsNoTracking().Where(entity => entity.Active);

        if (!string.IsNullOrWhiteSpace(normalizedName))
        {
            playersQuery = playersQuery.Where(entity =>
                EF.Functions.Like(entity.SearchFullNameNormalized, $"%{normalizedName}%"));
        }

        if (!string.IsNullOrWhiteSpace(normalizedTeam))
        {
            playersQuery = playersQuery.Where(entity =>
                (entity.TeamAbbr ?? string.Empty).ToUpper() == normalizedTeam
                || (entity.Team ?? string.Empty).ToUpper() == normalizedTeam);
        }

        if (!string.IsNullOrWhiteSpace(normalizedPosition))
        {
            playersQuery = playersQuery.Where(entity =>
                (entity.Position ?? string.Empty).ToUpper() == normalizedPosition
                || EF.Functions.Like(entity.FantasyPositionsTokenized, $"%|{normalizedPosition}|%"));
        }

        if (query.ByeWeek.HasValue)
        {
            playersQuery = playersQuery.Where(entity => entity.ByeWeek == query.ByeWeek.Value);
        }

        if (query.MinProjectedPoints.HasValue)
        {
            playersQuery = playersQuery.Where(entity =>
                entity.ProjectedFantasyPoints >= query.MinProjectedPoints.Value);
        }

        if (query.MaxAverageDraftPosition.HasValue)
        {
            playersQuery = playersQuery.Where(entity =>
                entity.AverageDraftPosition != null
                && entity.AverageDraftPosition <= query.MaxAverageDraftPosition.Value);
        }

        IOrderedQueryable<PlayerEntity> orderedQuery = query.SortBy?.ToLowerInvariant() switch
        {
            "projectedpoints" => query.SortDescending
                ? playersQuery.OrderByDescending(e => e.ProjectedFantasyPoints)
                : playersQuery.OrderBy(e => e.ProjectedFantasyPoints),
            "adp" => query.SortDescending
                ? playersQuery.OrderByDescending(e => e.AverageDraftPosition)
                : playersQuery.OrderBy(e => e.AverageDraftPosition),
            "lastseasonpoints" => query.SortDescending
                ? playersQuery.OrderByDescending(e => e.LastSeasonFantasyPoints)
                : playersQuery.OrderBy(e => e.LastSeasonFantasyPoints),
            "auctionvalue" => query.SortDescending
                ? playersQuery.OrderByDescending(e => e.AuctionValue)
                : playersQuery.OrderBy(e => e.AuctionValue),
            _ => query.SortDescending
                ? playersQuery.OrderByDescending(e => e.FullName ?? e.SleeperPlayerId)
                : playersQuery.OrderBy(e => e.FullName ?? e.SleeperPlayerId)
        };

        var players = await orderedQuery
            .ThenBy(entity => entity.SleeperPlayerId)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return players.Select(MapPlayer).ToArray();
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

    private static PlayerRecord MapPlayer(PlayerEntity entity)
    {
        var sleeperPlayer =
            JsonSerializer.Deserialize<SleeperPlayer>(entity.RawJson)
            ?? throw new InvalidOperationException(
                $"Unable to deserialize stored player payload for {entity.SleeperPlayerId}.");

        return new PlayerRecord
        {
            SleeperPlayerId = entity.SleeperPlayerId,
            YahooId = entity.YahooId,
            FantasyDataId = entity.FantasyDataId,
            FullName = entity.FullName,
            FirstName = entity.FirstName,
            LastName = entity.LastName,
            Team = entity.Team,
            TeamAbbr = entity.TeamAbbr,
            Position = entity.Position,
            FantasyPositions = ParseFantasyPositions(entity.FantasyPositionsTokenized),
            Status = entity.Status,
            Active = entity.Active,
            Sport = entity.Sport,
            SearchFullNameNormalized = entity.SearchFullNameNormalized,
            FantasyPositionsTokenized = entity.FantasyPositionsTokenized,
            RawJson = entity.RawJson,
            Data = sleeperPlayer,
            AverageDraftPosition = entity.AverageDraftPosition,
            ByeWeek = entity.ByeWeek,
            LastSeasonFantasyPoints = entity.LastSeasonFantasyPoints,
            ProjectedFantasyPoints = entity.ProjectedFantasyPoints,
            AuctionValue = entity.AuctionValue
        };
    }

    private static IReadOnlyList<string> ParseFantasyPositions(string tokenizedFantasyPositions)
    {
        return tokenizedFantasyPositions.Split(
            '|',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
