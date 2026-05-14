using LeagueAPI.Data;
using LeagueAPI.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace LeagueAPI.Services;

internal static class WaiverClaimStatus
{
    public const string Pending = "Pending";
    public const string Successful = "Successful";
    public const string Failed = "Failed";
    public const string Superseded = "Superseded";
}

internal static class WaiverProcessRunStatus
{
    public const string InProgress = "InProgress";
    public const string Succeeded = "Succeeded";
    public const string Failed = "Failed";
}

public sealed class PostgresWaiverService(IDbContextFactory<LeagueApiDbContext> dbContextFactory) : IWaiverService
{
    private readonly IDbContextFactory<LeagueApiDbContext> _dbContextFactory = dbContextFactory;

    public async Task SeedWaiverPriorityAsync(IReadOnlyList<string> draftOrder, bool force, CancellationToken cancellationToken)
    {
        if (draftOrder is null || draftOrder.Count == 0)
            throw new ArgumentException("Draft order must contain at least one agent.", nameof(draftOrder));

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var existingCount = await dbContext.WaiverPriorities.CountAsync(cancellationToken);
        if (existingCount > 0 && !force)
            return;

        // Reverse draft order: last pick gets best waiver priority (priority 1)
        var reversedOrder = draftOrder.Reverse().ToList();

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        if (existingCount > 0)
        {
            await dbContext.WaiverPriorities.ExecuteDeleteAsync(cancellationToken);
        }

        var now = DateTimeOffset.UtcNow;
        for (int i = 0; i < reversedOrder.Count; i++)
        {
            dbContext.WaiverPriorities.Add(new WaiverPriorityEntity
            {
                AgentId = reversedOrder[i].Trim(),
                Priority = i + 1,
                UpdatedAtUtc = now
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WaiverClaimResult>> SubmitWaiverClaimsAsync(string agentId, int season, int week, IReadOnlyList<WaiverClaimItem> claims, CancellationToken cancellationToken)
    {
        var normalizedAgentId = NormalizeRequired(agentId, nameof(agentId));

        if (claims is null || claims.Count == 0)
            throw new ArgumentException("At least one claim is required.", nameof(claims));

        ValidateClaimList(claims);

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        // Validate all add players exist, are active, and roster-eligible
        foreach (var claim in claims)
        {
            var addPlayer = await dbContext.Players
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.SleeperPlayerId == claim.AddSleeperPlayerId && p.Active, cancellationToken)
                ?? throw new ArgumentException($"Add player '{claim.AddSleeperPlayerId}' is not an active player.", nameof(claims));

            if (!RosterSlotRules.CanPlayerBeRostered(addPlayer.Position, addPlayer.FantasyPositionsTokenized))
                throw new ArgumentException($"Add player '{addPlayer.FullName ?? claim.AddSleeperPlayerId}' (position: {addPlayer.Position}) is not eligible for roster slots.", nameof(claims));

            var dropOwned = await dbContext.RosterAssignments
                .AsNoTracking()
                .AnyAsync(a => a.SleeperPlayerId == claim.DropSleeperPlayerId && a.AgentId == normalizedAgentId, cancellationToken);

            if (!dropOwned)
                throw new ArgumentException($"Drop player '{claim.DropSleeperPlayerId}' is not on agent '{normalizedAgentId}' roster.", nameof(claims));
        }

        // Get current priority for audit
        var priorityEntry = await dbContext.WaiverPriorities
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.AgentId == normalizedAgentId, cancellationToken);
        var currentPriority = priorityEntry?.Priority ?? 0;

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        // Replace pending claims for this agent/week
        await dbContext.WaiverClaims
            .Where(c => c.AgentId == normalizedAgentId && c.Season == season && c.Week == week && c.Status == WaiverClaimStatus.Pending)
            .ExecuteDeleteAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var entities = claims.Select(claim => new WaiverClaimEntity
        {
            WaiverClaimId = Guid.NewGuid(),
            AgentId = normalizedAgentId,
            Season = season,
            Week = week,
            ClaimOrder = claim.ClaimOrder,
            AddSleeperPlayerId = claim.AddSleeperPlayerId.Trim(),
            DropSleeperPlayerId = claim.DropSleeperPlayerId.Trim(),
            PriorityAtSubmission = currentPriority,
            Status = WaiverClaimStatus.Pending,
            SubmittedAtUtc = now
        }).ToList();

        dbContext.WaiverClaims.AddRange(entities);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return entities.Select(MapClaimToResult).ToList();
    }

    public async Task<ProcessWaiverClaimsResult> ProcessWaiverClaimsAsync(int season, int week, CancellationToken cancellationToken)
    {
        await using var setupDbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        // Prevent double-processing
        var existingRun = await setupDbContext.WaiverProcessRuns
            .AsNoTracking()
            .Where(r => r.Season == season && r.Week == week)
            .OrderByDescending(r => r.StartedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (existingRun?.Status == WaiverProcessRunStatus.Succeeded)
            throw new InvalidOperationException($"Waiver claims for season {season} week {week} have already been processed.");

        if (existingRun?.Status == WaiverProcessRunStatus.InProgress)
            throw new InvalidOperationException($"Waiver processing for season {season} week {week} is already in progress.");

        // Create process run record
        var processRunId = Guid.NewGuid();
        var processRun = new WaiverProcessRunEntity
        {
            WaiverProcessRunId = processRunId,
            Season = season,
            Week = week,
            Status = WaiverProcessRunStatus.InProgress,
            StartedAtUtc = DateTimeOffset.UtcNow
        };

        setupDbContext.WaiverProcessRuns.Add(processRun);
        await setupDbContext.SaveChangesAsync(cancellationToken);

        try
        {
            // Load priority order and pending claims
            var priorityOrder = await setupDbContext.WaiverPriorities
                .AsNoTracking()
                .OrderBy(p => p.Priority)
                .Select(p => p.AgentId)
                .ToListAsync(cancellationToken);

            var allPendingClaims = await setupDbContext.WaiverClaims
                .AsNoTracking()
                .Where(c => c.Season == season && c.Week == week && c.Status == WaiverClaimStatus.Pending)
                .OrderBy(c => c.ClaimOrder)
                .ToListAsync(cancellationToken);

            var claimsByAgent = allPendingClaims
                .GroupBy(c => c.AgentId)
                .ToDictionary(g => g.Key, g => g.OrderBy(c => c.ClaimOrder).ToList());

            // Include agents with claims who may not be in priority table (put them last)
            var agentsWithClaims = allPendingClaims.Select(c => c.AgentId).Distinct();
            var orderedAgents = priorityOrder
                .Concat(agentsWithClaims.Where(a => !priorityOrder.Contains(a, StringComparer.Ordinal)))
                .ToList();

            var succeededAgents = new List<string>();

            // Process claims in priority order
            foreach (var agentId in orderedAgents)
            {
                if (!claimsByAgent.TryGetValue(agentId, out var agentClaims))
                    continue;

                bool agentSucceeded = false;

                foreach (var claim in agentClaims)
                {
                    if (agentSucceeded)
                    {
                        await UpdateClaimStatusAsync(claim.WaiverClaimId, WaiverClaimStatus.Superseded, "Agent already received a waiver claim this period.", cancellationToken);
                        continue;
                    }

                    var (success, failureReason) = await TryExecuteClaimTransactionAsync(claim, cancellationToken);

                    if (success)
                    {
                        agentSucceeded = true;
                        succeededAgents.Add(agentId);
                    }
                    else
                    {
                        await UpdateClaimStatusAsync(claim.WaiverClaimId, WaiverClaimStatus.Failed, failureReason, cancellationToken);
                    }
                }
            }

            // Update rolling priority
            await UpdateRollingPriorityAsync(priorityOrder, succeededAgents, cancellationToken);

            // Load all processed claims for the result
            await using var resultDbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

            var processedClaims = await resultDbContext.WaiverClaims
                .AsNoTracking()
                .Where(c => c.Season == season && c.Week == week && c.Status != WaiverClaimStatus.Pending)
                .OrderBy(c => c.AgentId).ThenBy(c => c.ClaimOrder)
                .ToListAsync(cancellationToken);

            int succeeded = processedClaims.Count(c => c.Status == WaiverClaimStatus.Successful);
            int failed = processedClaims.Count(c => c.Status == WaiverClaimStatus.Failed || c.Status == WaiverClaimStatus.Superseded);

            // Complete process run
            var completedRun = await resultDbContext.WaiverProcessRuns.FindAsync([processRunId], cancellationToken)
                ?? throw new InvalidOperationException("Process run record not found after processing.");

            completedRun.Status = WaiverProcessRunStatus.Succeeded;
            completedRun.ClaimsProcessed = processedClaims.Count;
            completedRun.ClaimsSucceeded = succeeded;
            completedRun.ClaimsFailed = failed;
            completedRun.CompletedAtUtc = DateTimeOffset.UtcNow;
            await resultDbContext.SaveChangesAsync(cancellationToken);

            return new ProcessWaiverClaimsResult(
                processRunId,
                season,
                week,
                WaiverProcessRunStatus.Succeeded,
                processedClaims.Count,
                succeeded,
                failed,
                null,
                completedRun.StartedAtUtc,
                completedRun.CompletedAtUtc,
                processedClaims.Select(MapClaimToResult).ToList());
        }
        catch (Exception ex)
        {
            // Mark process run as failed and clean up any remaining Pending claims
            try
            {
                await using var failDbContext = await _dbContextFactory.CreateDbContextAsync(CancellationToken.None);
                var failRun = await failDbContext.WaiverProcessRuns.FindAsync([processRunId], CancellationToken.None);
                if (failRun is not null)
                {
                    failRun.Status = WaiverProcessRunStatus.Failed;
                    failRun.ErrorMessage = ex.Message;
                    failRun.CompletedAtUtc = DateTimeOffset.UtcNow;
                    await failDbContext.SaveChangesAsync(CancellationToken.None);
                }

                // Mark remaining Pending claims as Failed so they don't linger
                await failDbContext.WaiverClaims
                    .Where(c => c.Season == season && c.Week == week && c.Status == WaiverClaimStatus.Pending)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(c => c.Status, WaiverClaimStatus.Failed)
                        .SetProperty(c => c.FailureReason, "Processing run failed before this claim was evaluated.")
                        .SetProperty(c => c.ProcessedAtUtc, DateTimeOffset.UtcNow), CancellationToken.None);
            }
            catch { /* best-effort */ }

            throw;
        }
    }

    public async Task<AddFreeAgentResult> AddFreeAgentAsync(string agentId, int season, int week, string addSleeperPlayerId, string dropSleeperPlayerId, CancellationToken cancellationToken)
    {
        var normalizedAgentId = NormalizeRequired(agentId, nameof(agentId));
        var normalizedAddId = NormalizeRequired(addSleeperPlayerId, nameof(addSleeperPlayerId));
        var normalizedDropId = NormalizeRequired(dropSleeperPlayerId, nameof(dropSleeperPlayerId));

        if (string.Equals(normalizedAddId, normalizedDropId, StringComparison.Ordinal))
            throw new ArgumentException("Add and drop players must be different.", nameof(addSleeperPlayerId));

        // Verify waivers have been processed for this week
        var status = await GetWaiverProcessStatusAsync(season, week, cancellationToken);
        if (!status.HasBeenProcessed)
            throw new InvalidOperationException($"Waiver claims for season {season} week {week} have not been processed yet. Free agent adds are only allowed after waivers have been processed.");

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);

        var addPlayer = await dbContext.Players
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.SleeperPlayerId == normalizedAddId && p.Active, cancellationToken)
            ?? throw new RosterPlayerNotFoundException($"Active player '{normalizedAddId}' was not found.");

        if (!RosterSlotRules.CanPlayerBeRostered(addPlayer.Position, addPlayer.FantasyPositionsTokenized))
            throw new ArgumentException($"Player '{addPlayer.FullName ?? normalizedAddId}' (position: {addPlayer.Position}) is not eligible for roster slots.", nameof(addSleeperPlayerId));

        var existingOwner = await dbContext.RosterAssignments
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.SleeperPlayerId == normalizedAddId, cancellationToken);

        if (existingOwner is not null)
            throw new RosterConflictException($"Player '{normalizedAddId}' is already on a roster.");

        var dropAssignment = await dbContext.RosterAssignments
            .FirstOrDefaultAsync(a => a.SleeperPlayerId == normalizedDropId && a.AgentId == normalizedAgentId, cancellationToken)
            ?? throw new RosterPlayerNotFoundException($"Drop player '{normalizedDropId}' is not on agent '{normalizedAgentId}' roster.");

        dbContext.RosterAssignments.Remove(dropAssignment);
        var acquiredAt = DateTimeOffset.UtcNow;
        dbContext.RosterAssignments.Add(new RosterAssignmentEntity
        {
            RosterAssignmentId = Guid.NewGuid(),
            AgentId = normalizedAgentId,
            SleeperPlayerId = normalizedAddId,
            AcquiredAtUtc = acquiredAt,
            AcquisitionSource = "free-agent",
            SlotType = RosterSlotRules.BenchSlot
        });

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            throw new RosterConflictException($"Player '{normalizedAddId}' was added to another roster before this request completed.", ex);
        }

        return new AddFreeAgentResult(normalizedAgentId, normalizedAddId, normalizedDropId, acquiredAt);
    }

    public async Task<IReadOnlyList<WaiverClaimResult>> GetWaiverClaimsAsync(int season, int week, string? agentId, CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var query = dbContext.WaiverClaims
            .AsNoTracking()
            .Where(c => c.Season == season && c.Week == week);

        if (!string.IsNullOrWhiteSpace(agentId))
            query = query.Where(c => c.AgentId == agentId.Trim());

        var claims = await query
            .OrderBy(c => c.AgentId)
            .ThenBy(c => c.ClaimOrder)
            .ToListAsync(cancellationToken);

        return claims.Select(MapClaimToResult).ToList();
    }

    public async Task<WaiverPriorityResult> GetWaiverPriorityAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var priorities = await dbContext.WaiverPriorities
            .AsNoTracking()
            .OrderBy(p => p.Priority)
            .ToListAsync(cancellationToken);

        return new WaiverPriorityResult(
            priorities.Select(p => new WaiverPriorityEntry(p.AgentId, p.Priority, p.UpdatedAtUtc)).ToList());
    }

    public async Task<WaiverProcessStatusResult> GetWaiverProcessStatusAsync(int season, int week, CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var run = await dbContext.WaiverProcessRuns
            .AsNoTracking()
            .Where(r => r.Season == season && r.Week == week && r.Status == WaiverProcessRunStatus.Succeeded)
            .OrderByDescending(r => r.CompletedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        return run is null
            ? new WaiverProcessStatusResult(season, week, false, null, null, 0, 0, null)
            : new WaiverProcessStatusResult(season, week, true, run.WaiverProcessRunId, run.Status, run.ClaimsSucceeded, run.ClaimsFailed, run.CompletedAtUtc);
    }

    // --- Private helpers ---

    private async Task<(bool Success, string? FailureReason)> TryExecuteClaimTransactionAsync(WaiverClaimEntity claim, CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);

        try
        {
            var addPlayer = await dbContext.Players
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.SleeperPlayerId == claim.AddSleeperPlayerId && p.Active, cancellationToken);

            if (addPlayer is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return (false, $"Add player '{claim.AddSleeperPlayerId}' is not an active player.");
            }

            var addOwned = await dbContext.RosterAssignments
                .AsNoTracking()
                .AnyAsync(a => a.SleeperPlayerId == claim.AddSleeperPlayerId, cancellationToken);

            if (addOwned)
            {
                await transaction.RollbackAsync(cancellationToken);
                return (false, $"Player '{claim.AddSleeperPlayerId}' is already on a roster.");
            }

            var dropAssignment = await dbContext.RosterAssignments
                .FirstOrDefaultAsync(a => a.SleeperPlayerId == claim.DropSleeperPlayerId && a.AgentId == claim.AgentId, cancellationToken);

            if (dropAssignment is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return (false, $"Drop player '{claim.DropSleeperPlayerId}' is no longer on agent '{claim.AgentId}' roster.");
            }

            dbContext.RosterAssignments.Remove(dropAssignment);
            dbContext.RosterAssignments.Add(new RosterAssignmentEntity
            {
                RosterAssignmentId = Guid.NewGuid(),
                AgentId = claim.AgentId,
                SleeperPlayerId = claim.AddSleeperPlayerId,
                AcquiredAtUtc = DateTimeOffset.UtcNow,
                AcquisitionSource = "waiver",
                SlotType = RosterSlotRules.BenchSlot
            });

            await dbContext.SaveChangesAsync(cancellationToken);

            // Mark claim as successful within the same transaction
            var claimEntity = await dbContext.WaiverClaims
                .FirstOrDefaultAsync(c => c.WaiverClaimId == claim.WaiverClaimId, cancellationToken)
                ?? throw new InvalidOperationException($"Claim '{claim.WaiverClaimId}' disappeared during waiver processing.");

            claimEntity.Status = WaiverClaimStatus.Successful;
            claimEntity.ProcessedAtUtc = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return (true, null);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            try { await transaction.RollbackAsync(CancellationToken.None); } catch { /* best-effort */ }
            return (false, "Concurrent roster conflict — another agent may have claimed this player simultaneously.");
        }
        catch (PostgresException ex) when (ex.SqlState == "40001")
        {
            // Serialization failure — treat as a non-fatal claim failure
            try { await transaction.RollbackAsync(CancellationToken.None); } catch { /* best-effort */ }
            return (false, "Serialization conflict during processing — claim was skipped.");
        }
    }

    private async Task UpdateClaimStatusAsync(Guid claimId, string status, string? failureReason, CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var claim = await dbContext.WaiverClaims.FindAsync([claimId], cancellationToken);
        if (claim is null) return;

        claim.Status = status;
        claim.FailureReason = failureReason;
        claim.ProcessedAtUtc = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task UpdateRollingPriorityAsync(IReadOnlyList<string> currentPriorityOrder, IReadOnlyList<string> succeededAgents, CancellationToken cancellationToken)
    {
        if (succeededAgents.Count == 0) return;

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var allPriorities = await dbContext.WaiverPriorities
            .OrderBy(p => p.Priority)
            .ToListAsync(cancellationToken);

        // Remaining agents keep relative order; succeeded agents append at end in success order
        var remaining = currentPriorityOrder
            .Where(id => !succeededAgents.Contains(id, StringComparer.Ordinal))
            .ToList();

        // Include any agents in the priority table not in the original order (edge case)
        foreach (var extra in allPriorities.Select(p => p.AgentId).Where(id => !remaining.Contains(id, StringComparer.Ordinal) && !succeededAgents.Contains(id, StringComparer.Ordinal)))
            remaining.Add(extra);

        var newOrder = remaining.Concat(succeededAgents).ToList();

        // Delete all and reinsert to avoid unique index conflicts
        dbContext.WaiverPriorities.RemoveRange(allPriorities);
        await dbContext.SaveChangesAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        for (int i = 0; i < newOrder.Count; i++)
        {
            dbContext.WaiverPriorities.Add(new WaiverPriorityEntity
            {
                AgentId = newOrder[i],
                Priority = i + 1,
                UpdatedAtUtc = now
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static void ValidateClaimList(IReadOnlyList<WaiverClaimItem> claims)
    {
        var claimOrders = claims.Select(c => c.ClaimOrder).ToList();
        if (claimOrders.Distinct().Count() != claimOrders.Count)
            throw new ArgumentException("Claim order values must be unique within a claim list.", nameof(claims));

        var addPlayerIds = claims.Select(c => c.AddSleeperPlayerId?.Trim()).ToList();
        if (addPlayerIds.Distinct(StringComparer.Ordinal).Count() != addPlayerIds.Count)
            throw new ArgumentException("Each claim must target a different add player.", nameof(claims));

        foreach (var claim in claims)
        {
            if (string.IsNullOrWhiteSpace(claim.AddSleeperPlayerId))
                throw new ArgumentException("AddSleeperPlayerId is required on every claim.", nameof(claims));

            if (string.IsNullOrWhiteSpace(claim.DropSleeperPlayerId))
                throw new ArgumentException("DropSleeperPlayerId is required on every claim.", nameof(claims));

            if (string.Equals(claim.AddSleeperPlayerId.Trim(), claim.DropSleeperPlayerId.Trim(), StringComparison.Ordinal))
                throw new ArgumentException($"Add and drop player must be different (claim order {claim.ClaimOrder}).", nameof(claims));
        }
    }

    private static WaiverClaimResult MapClaimToResult(WaiverClaimEntity entity) =>
        new(entity.WaiverClaimId, entity.AgentId, entity.Season, entity.Week, entity.ClaimOrder,
            entity.AddSleeperPlayerId, entity.DropSleeperPlayerId, entity.PriorityAtSubmission,
            entity.Status, entity.FailureReason, entity.SubmittedAtUtc, entity.ProcessedAtUtc);

    private static string NormalizeRequired(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{paramName} is required.", paramName);

        return value.Trim();
    }

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    public async Task<MyWaiverStatusResult> GetMyWaiverStatusAsync(string agentId, int season, int week, CancellationToken cancellationToken)
    {
        agentId = NormalizeRequired(agentId, nameof(agentId));

        var priorityTask = GetWaiverPriorityAsync(cancellationToken);
        var claimsTask = GetWaiverClaimsAsync(season, week, agentId, cancellationToken);
        var statusTask = GetWaiverProcessStatusAsync(season, week, cancellationToken);

        await Task.WhenAll(priorityTask, claimsTask, statusTask);

        var priority = await priorityTask;
        var myClaims = await claimsTask;
        var processStatus = await statusTask;

        var myPriority = priority.Priority.FirstOrDefault(p => p.AgentId == agentId)?.Priority;
        var hasPending = myClaims.Any(c => c.Status == WaiverClaimStatus.Pending);
        var phase = processStatus.HasBeenProcessed ? "free_agency" : "waiver_window";

        return new MyWaiverStatusResult(
            Season: season,
            Week: week,
            Phase: phase,
            MyPriority: myPriority,
            TotalAgents: priority.Priority.Count,
            HasPendingClaims: hasPending,
            MyClaims: myClaims,
            WaiversProcessedAtUtc: processStatus.CompletedAtUtc);
    }
}
