using LeagueAPI.Data;
using LeagueAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace LeagueAPI.Services;

public sealed class PostgresRosterStore(IDbContextFactory<LeagueApiDbContext> dbContextFactory) : IRosterReader, IRosterWriter
{
    private readonly IDbContextFactory<LeagueApiDbContext> _dbContextFactory = dbContextFactory;

    public async Task<IReadOnlyList<RosterPlayerResult>> GetRosterAsync(
        string agentId,
        CancellationToken cancellationToken)
    {
        var normalizedAgentId = NormalizeAgentId(agentId);

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var results = await (
            from assignment in dbContext.RosterAssignments.AsNoTracking()
            join player in dbContext.Players.AsNoTracking().Where(entity => entity.Active)
                on assignment.SleeperPlayerId equals player.SleeperPlayerId
            where assignment.AgentId == normalizedAgentId
            orderby player.FullName ?? player.SleeperPlayerId, player.SleeperPlayerId
            select new
            {
                Player = player,
                assignment.AgentId,
                assignment.AcquiredAtUtc,
                assignment.AcquisitionSource
            })
            .ToListAsync(cancellationToken);

        return results
            .Select(result => new RosterPlayerResult(
                PlayerRecordFactory.Map(result.Player),
                result.AgentId,
                IsAvailable: false,
                result.AcquiredAtUtc,
                result.AcquisitionSource))
            .ToList();
    }

    public async Task<IReadOnlyList<RosterPlayerResult>> QueryPlayersAsync(
        PlayerQuery query,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var normalizedLimit = PlayerCatalogQueryBuilder.NormalizeLimit(query.Limit);
        var filteredPlayers = PlayerCatalogQueryBuilder.ApplyFilters(
            dbContext.Players.AsNoTracking().Where(entity => entity.Active),
            query);
        var orderedPlayers = PlayerCatalogQueryBuilder.ApplyOrdering(filteredPlayers, query)
            .ThenBy(entity => entity.SleeperPlayerId);

        var results = await (
            from player in orderedPlayers
            join assignment in dbContext.RosterAssignments.AsNoTracking()
                on player.SleeperPlayerId equals assignment.SleeperPlayerId into assignmentGroup
            from assignment in assignmentGroup.DefaultIfEmpty()
            select new PlayerOwnershipRow(
                player,
                assignment != null ? assignment.AgentId : null,
                assignment != null ? assignment.AcquiredAtUtc : (DateTimeOffset?)null,
                assignment != null ? assignment.AcquisitionSource : null))
            .Take(normalizedLimit)
            .ToListAsync(cancellationToken);

        return results
            .Select(MapPlayerResult)
            .ToList();
    }

    public async Task<IReadOnlyList<RosterPlayerResult>> GetAvailablePlayersAsync(
        PlayerQuery query,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var normalizedLimit = PlayerCatalogQueryBuilder.NormalizeLimit(query.Limit);
        var filteredPlayers = PlayerCatalogQueryBuilder.ApplyFilters(
            dbContext.Players.AsNoTracking().Where(entity => entity.Active),
            query);
        var orderedPlayers = PlayerCatalogQueryBuilder.ApplyOrdering(filteredPlayers, query)
            .ThenBy(entity => entity.SleeperPlayerId);

        var players = await (
            from player in orderedPlayers
            join assignment in dbContext.RosterAssignments.AsNoTracking()
                on player.SleeperPlayerId equals assignment.SleeperPlayerId into assignmentGroup
            from assignment in assignmentGroup.DefaultIfEmpty()
            where assignment == null
            select player)
            .Take(normalizedLimit)
            .ToListAsync(cancellationToken);

        return players
            .Select(player => new RosterPlayerResult(
                PlayerRecordFactory.Map(player),
                OwnerAgentId: null,
                IsAvailable: true,
                AcquiredAtUtc: null,
                AcquisitionSource: null))
            .ToList();
    }

    public async Task<RosterPlayerResult?> GetPlayerAvailabilityAsync(
        string sleeperPlayerId,
        CancellationToken cancellationToken)
    {
        var normalizedSleeperPlayerId = NormalizeSleeperPlayerId(sleeperPlayerId);

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var result = await (
            from player in dbContext.Players.AsNoTracking()
            join assignment in dbContext.RosterAssignments.AsNoTracking()
                on player.SleeperPlayerId equals assignment.SleeperPlayerId into assignmentGroup
            from assignment in assignmentGroup.DefaultIfEmpty()
            where player.Active && player.SleeperPlayerId == normalizedSleeperPlayerId
            select new PlayerOwnershipRow(
                player,
                assignment != null ? assignment.AgentId : null,
                assignment != null ? assignment.AcquiredAtUtc : (DateTimeOffset?)null,
                assignment != null ? assignment.AcquisitionSource : null))
            .FirstOrDefaultAsync(cancellationToken);

        return result is null ? null : MapPlayerResult(result);
    }

    public async Task<RosterPlayerResult> AddPlayerToRosterAsync(
        string agentId,
        string sleeperPlayerId,
        string acquisitionSource,
        CancellationToken cancellationToken)
    {
        var normalizedAgentId = NormalizeAgentId(agentId);
        var normalizedSleeperPlayerId = NormalizeSleeperPlayerId(sleeperPlayerId);
        var normalizedAcquisitionSource = NormalizeAcquisitionSource(acquisitionSource);

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var player = await dbContext.Players
            .AsNoTracking()
            .FirstOrDefaultAsync(
                entity => entity.SleeperPlayerId == normalizedSleeperPlayerId && entity.Active,
                cancellationToken)
            ?? throw new RosterPlayerNotFoundException(
                $"Active player '{normalizedSleeperPlayerId}' was not found.");

        var existingAssignment = await dbContext.RosterAssignments
            .AsNoTracking()
            .FirstOrDefaultAsync(
                assignment => assignment.SleeperPlayerId == normalizedSleeperPlayerId,
                cancellationToken);

        if (existingAssignment is not null)
        {
            throw CreateConflictException(normalizedAgentId, normalizedSleeperPlayerId, existingAssignment.AgentId);
        }

        var acquiredAtUtc = DateTimeOffset.UtcNow;

        dbContext.RosterAssignments.Add(new RosterAssignmentEntity
        {
            RosterAssignmentId = Guid.NewGuid(),
            AgentId = normalizedAgentId,
            SleeperPlayerId = normalizedSleeperPlayerId,
            AcquiredAtUtc = acquiredAtUtc,
            AcquisitionSource = normalizedAcquisitionSource
        });

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            var conflictingAssignment = await dbContext.RosterAssignments
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    assignment => assignment.SleeperPlayerId == normalizedSleeperPlayerId,
                    cancellationToken);

            if (conflictingAssignment is not null)
            {
                throw CreateConflictException(
                    normalizedAgentId,
                    normalizedSleeperPlayerId,
                    conflictingAssignment.AgentId,
                    ex);
            }

            throw;
        }

        return new RosterPlayerResult(
            PlayerRecordFactory.Map(player),
            normalizedAgentId,
            IsAvailable: false,
            acquiredAtUtc,
            normalizedAcquisitionSource);
    }

    public async Task<RosterPlayerResult> RemovePlayerFromRosterAsync(
        string agentId,
        string sleeperPlayerId,
        CancellationToken cancellationToken)
    {
        var normalizedAgentId = NormalizeAgentId(agentId);
        var normalizedSleeperPlayerId = NormalizeSleeperPlayerId(sleeperPlayerId);

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var assignment = await dbContext.RosterAssignments
            .FirstOrDefaultAsync(
                row => row.SleeperPlayerId == normalizedSleeperPlayerId,
                cancellationToken);

        if (assignment is null)
        {
            throw new RosterPlayerNotFoundException(
                $"Player '{normalizedSleeperPlayerId}' is not currently on a roster.");
        }

        if (!string.Equals(assignment.AgentId, normalizedAgentId, StringComparison.Ordinal))
        {
            throw CreateConflictException(normalizedAgentId, normalizedSleeperPlayerId, assignment.AgentId);
        }

        var player = await dbContext.Players
            .AsNoTracking()
            .FirstOrDefaultAsync(
                entity => entity.SleeperPlayerId == normalizedSleeperPlayerId,
                cancellationToken)
            ?? throw new RosterPlayerNotFoundException(
                $"Player '{normalizedSleeperPlayerId}' could not be loaded for roster removal.");

        dbContext.RosterAssignments.Remove(assignment);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new RosterPlayerResult(
            PlayerRecordFactory.Map(player),
            OwnerAgentId: null,
            IsAvailable: true,
            AcquiredAtUtc: null,
            AcquisitionSource: null);
    }

    private static RosterPlayerResult MapPlayerResult(PlayerOwnershipRow result)
    {
        return new RosterPlayerResult(
            PlayerRecordFactory.Map(result.Player),
            result.OwnerAgentId,
            result.OwnerAgentId is null,
            result.AcquiredAtUtc,
            result.AcquisitionSource);
    }

    private static string NormalizeAgentId(string agentId)
    {
        if (string.IsNullOrWhiteSpace(agentId))
        {
            throw new ArgumentException("Agent ID is required.", nameof(agentId));
        }

        return agentId.Trim();
    }

    private static string NormalizeSleeperPlayerId(string sleeperPlayerId)
    {
        if (string.IsNullOrWhiteSpace(sleeperPlayerId))
        {
            throw new ArgumentException("Sleeper player ID is required.", nameof(sleeperPlayerId));
        }

        return sleeperPlayerId.Trim();
    }

    private static string NormalizeAcquisitionSource(string acquisitionSource)
    {
        if (string.IsNullOrWhiteSpace(acquisitionSource))
        {
            throw new ArgumentException("Acquisition source is required.", nameof(acquisitionSource));
        }

        var normalizedSource = acquisitionSource.Trim().ToLowerInvariant();
        if (normalizedSource.Length > 32)
        {
            throw new ArgumentException(
                "Acquisition source must be 32 characters or fewer.",
                nameof(acquisitionSource));
        }

        return normalizedSource;
    }

    private static RosterConflictException CreateConflictException(
        string requestedAgentId,
        string sleeperPlayerId,
        string owningAgentId,
        Exception? innerException = null)
    {
        if (string.Equals(requestedAgentId, owningAgentId, StringComparison.Ordinal))
        {
            return new RosterConflictException(
                $"Player '{sleeperPlayerId}' is already on roster '{requestedAgentId}'.",
                innerException);
        }

        return new RosterConflictException(
            $"Player '{sleeperPlayerId}' is already owned by agent '{owningAgentId}'.",
            innerException);
    }

    private sealed record PlayerOwnershipRow(
        PlayerEntity Player,
        string? OwnerAgentId,
        DateTimeOffset? AcquiredAtUtc,
        string? AcquisitionSource);
}
