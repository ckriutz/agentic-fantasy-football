using LeagueAPI.Data;
using LeagueAPI.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

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
                assignment.AcquisitionSource,
                assignment.SlotType
            })
            .ToListAsync(cancellationToken);

        return results
            .Select(result => new RosterPlayerResult(
                PlayerRecordFactory.Map(result.Player),
                result.AgentId,
                IsAvailable: false,
                result.AcquiredAtUtc,
                result.AcquisitionSource,
                result.SlotType ?? RosterSlotRules.BenchSlot,
                RosterSlotRules.IsStarterSlot(result.SlotType)))
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
                assignment != null ? assignment.AcquisitionSource : null,
                assignment != null ? assignment.SlotType : null))
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

        var players = await (
            from player in filteredPlayers
            join assignment in dbContext.RosterAssignments.AsNoTracking()
                on player.SleeperPlayerId equals assignment.SleeperPlayerId into assignmentGroup
            from assignment in assignmentGroup.DefaultIfEmpty()
            where assignment == null
            select player)
            .ToListAsync(cancellationToken);

        return players
            .Where(player => RosterSlotRules.CanPlayerBeRostered(player.Position, player.FantasyPositionsTokenized))
            .Select(PlayerRecordFactory.Map)
            .OrderBy(player => player.SearchRank ?? int.MaxValue)
            .ThenBy(player => player.FullName ?? player.SleeperPlayerId)
            .ThenBy(player => player.SleeperPlayerId)
            .Take(normalizedLimit)
            .Select(player => new RosterPlayerResult(
                player,
                OwnerAgentId: null,
                IsAvailable: true,
                AcquiredAtUtc: null,
                AcquisitionSource: null,
                SlotType: null,
                IsStarter: false))
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
                assignment != null ? assignment.AcquisitionSource : null,
                assignment != null ? assignment.SlotType : null))
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

        if (!RosterSlotRules.CanPlayerBeRostered(player.Position, player.FantasyPositionsTokenized))
        {
            throw new ArgumentException(
                $"Player '{player.FullName ?? normalizedSleeperPlayerId}' (position: {player.Position}) is not eligible for this league's roster slots.",
                nameof(sleeperPlayerId));
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable,
            cancellationToken);

        var existingAssignment = await dbContext.RosterAssignments
            .AsNoTracking()
            .FirstOrDefaultAsync(
                assignment => assignment.SleeperPlayerId == normalizedSleeperPlayerId,
                cancellationToken);

        if (existingAssignment is not null)
        {
            throw CreateConflictException(normalizedAgentId, normalizedSleeperPlayerId, existingAssignment.AgentId);
        }

        var currentRosterCount = await dbContext.RosterAssignments
            .AsNoTracking()
            .CountAsync(a => a.AgentId == normalizedAgentId, cancellationToken);

        if (currentRosterCount >= RosterSlotRules.MaxRosterSize)
        {
            throw new RosterConflictException(
                $"Agent '{normalizedAgentId}' already has {currentRosterCount} players on their roster. The maximum roster size is {RosterSlotRules.MaxRosterSize}.");
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
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            if (IsUniqueViolation(ex))
            {
                throw new RosterConflictException(
                    $"Player '{normalizedSleeperPlayerId}' was added to another roster before this request completed.",
                    ex);
            }

            throw;
        }

        return new RosterPlayerResult(
            PlayerRecordFactory.Map(player),
            normalizedAgentId,
            IsAvailable: false,
            acquiredAtUtc,
            normalizedAcquisitionSource,
            SlotType: RosterSlotRules.BenchSlot,
            IsStarter: false);
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
            AcquisitionSource: null,
            SlotType: null,
            IsStarter: false);
    }

    public async Task<RosterPlayerResult> SetPlayerSlotAsync(string agentId, string sleeperPlayerId, string slotType, CancellationToken cancellationToken)
    {
        var normalizedAgentId = NormalizeAgentId(agentId);
        var normalizedSleeperPlayerId = NormalizeSleeperPlayerId(sleeperPlayerId);
        var normalizedSlotType = RosterSlotRules.NormalizeSlotType(slotType);

        if (!RosterSlotRules.IsKnownSlotType(normalizedSlotType))
        {
            throw new ArgumentException(
                $"'{normalizedSlotType}' is not a valid slot type. Valid starter slots are: {string.Join(", ", RosterSlotRules.StarterSlots)}. Use '{RosterSlotRules.BenchSlot}' for bench.",
                nameof(slotType));
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var targetAssignment = await dbContext.RosterAssignments
            .FirstOrDefaultAsync(a => a.SleeperPlayerId == normalizedSleeperPlayerId, cancellationToken);

        if (targetAssignment is null)
        {
            throw new RosterPlayerNotFoundException(
                $"Player '{normalizedSleeperPlayerId}' is not currently on a roster.");
        }

        if (!string.Equals(targetAssignment.AgentId, normalizedAgentId, StringComparison.Ordinal))
        {
            throw new RosterConflictException(
                $"Player '{normalizedSleeperPlayerId}' is owned by agent '{targetAssignment.AgentId}', not '{normalizedAgentId}'.");
        }

        var player = await dbContext.Players
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.SleeperPlayerId == normalizedSleeperPlayerId, cancellationToken)
            ?? throw new RosterPlayerNotFoundException(
                $"Player '{normalizedSleeperPlayerId}' could not be loaded.");

        if (!RosterSlotRules.CanPlayerOccupySlot(normalizedSlotType, player.Position, player.FantasyPositionsTokenized))
        {
            var eligible = RosterSlotRules.GetEligibleStarterSlots(player.Position, player.FantasyPositionsTokenized);
            var eligibleDesc = eligible.Count > 0
                ? string.Join(", ", eligible)
                : RosterSlotRules.BenchSlot;
            throw new ArgumentException(
                $"Player '{player.FullName ?? normalizedSleeperPlayerId}' (position: {player.Position}) cannot be placed in slot '{normalizedSlotType}'. Eligible slots: {eligibleDesc}, {RosterSlotRules.BenchSlot}.",
                nameof(slotType));
        }

        if (RosterSlotRules.IsStarterSlot(normalizedSlotType))
        {
            var occupyingAssignment = await dbContext.RosterAssignments
                .FirstOrDefaultAsync(
                    a => a.AgentId == normalizedAgentId
                         && a.SlotType == normalizedSlotType
                         && a.SleeperPlayerId != normalizedSleeperPlayerId,
                    cancellationToken);

            if (occupyingAssignment is not null)
            {
                occupyingAssignment.SlotType = RosterSlotRules.BenchSlot;
            }
        }

        targetAssignment.SlotType = normalizedSlotType;
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            throw new RosterConflictException(
                $"Slot '{normalizedSlotType}' is already occupied on roster '{normalizedAgentId}'.",
                ex);
        }

        return new RosterPlayerResult(
            PlayerRecordFactory.Map(player),
            normalizedAgentId,
            IsAvailable: false,
            targetAssignment.AcquiredAtUtc,
            targetAssignment.AcquisitionSource,
            normalizedSlotType,
            RosterSlotRules.IsStarterSlot(normalizedSlotType));
    }

    public async Task<IReadOnlyList<RosterPlayerResult>> AutoSetLineupAsync(string agentId, CancellationToken cancellationToken)
    {
        var normalizedAgentId = NormalizeAgentId(agentId);

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var rosterRows = await (
            from assignment in dbContext.RosterAssignments
            join player in dbContext.Players.AsNoTracking().Where(entity => entity.Active)
                on assignment.SleeperPlayerId equals player.SleeperPlayerId
            where assignment.AgentId == normalizedAgentId
            select new RosterAssignmentPlayerRow(assignment, player))
            .ToListAsync(cancellationToken);

        foreach (var row in rosterRows)
        {
            row.Assignment.SlotType = RosterSlotRules.BenchSlot;
        }

        var remainingPlayers = rosterRows
            .Select(row => new AutoLineupCandidate(
                row.Assignment,
                row.Player,
                PlayerRecordFactory.Map(row.Player)))
            .OrderBy(candidate => candidate.PlayerRecord.SearchRank ?? int.MaxValue)
            .ThenBy(candidate => candidate.PlayerRecord.FullName ?? candidate.PlayerRecord.SleeperPlayerId)
            .ThenBy(candidate => candidate.PlayerRecord.SleeperPlayerId)
            .ToList();

        foreach (var slotType in RosterSlotRules.StarterSlots)
        {
            var selectedPlayer = remainingPlayers.FirstOrDefault(candidate =>
                RosterSlotRules.CanPlayerOccupySlot(
                    slotType,
                    candidate.Player.Position,
                    candidate.Player.FantasyPositionsTokenized));

            if (selectedPlayer is null)
            {
                continue;
            }

            selectedPlayer.Assignment.SlotType = slotType;
            remainingPlayers.Remove(selectedPlayer);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return rosterRows
            .OrderBy(row => RosterSlotRules.IsBenchSlot(row.Assignment.SlotType) ? 1 : 0)
            .ThenBy(row => GetSlotSortOrder(row.Assignment.SlotType))
            .ThenBy(row => row.Player.FullName ?? row.Player.SleeperPlayerId)
            .ThenBy(row => row.Player.SleeperPlayerId)
            .Select(row => new RosterPlayerResult(
                PlayerRecordFactory.Map(row.Player),
                normalizedAgentId,
                IsAvailable: false,
                row.Assignment.AcquiredAtUtc,
                row.Assignment.AcquisitionSource,
                row.Assignment.SlotType ?? RosterSlotRules.BenchSlot,
                RosterSlotRules.IsStarterSlot(row.Assignment.SlotType)))
            .ToList();
    }

    private static RosterPlayerResult MapPlayerResult(PlayerOwnershipRow result)
    {
        return new RosterPlayerResult(
            PlayerRecordFactory.Map(result.Player),
            result.OwnerAgentId,
            result.OwnerAgentId is null,
            result.AcquiredAtUtc,
            result.AcquisitionSource,
            result.SlotType ?? (result.OwnerAgentId is null ? null : RosterSlotRules.BenchSlot),
            RosterSlotRules.IsStarterSlot(result.SlotType));
    }

    private static string NormalizeAgentId(string agentId)
    {
        if (string.IsNullOrWhiteSpace(agentId))
        {
            throw new ArgumentException("Agent ID is required.", nameof(agentId));
        }

        return agentId.Trim();
    }

    private static int GetSlotSortOrder(string? slotType)
    {
        var normalizedSlotType = RosterSlotRules.NormalizeSlotType(slotType);
        var slotIndex = RosterSlotRules.StarterSlots
            .Select((slot, index) => new { slot, index })
            .FirstOrDefault(item => string.Equals(item.slot, normalizedSlotType, StringComparison.Ordinal));

        return slotIndex?.index ?? int.MaxValue;
    }

    private static bool IsUniqueViolation(DbUpdateException exception)
    {
        return exception.InnerException is PostgresException postgresException
               && postgresException.SqlState == PostgresErrorCodes.UniqueViolation;
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
        string? AcquisitionSource,
        string? SlotType);

    private sealed record RosterAssignmentPlayerRow(
        RosterAssignmentEntity Assignment,
        PlayerEntity Player);

    private sealed record AutoLineupCandidate(
        RosterAssignmentEntity Assignment,
        PlayerEntity Player,
        PlayerRecord PlayerRecord);
}
