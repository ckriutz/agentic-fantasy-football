using LeagueAPI.Data;
using LeagueAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace LeagueAPI.Services;

public sealed class LeagueRunnerRunStore(IDbContextFactory<LeagueApiDbContext> dbContextFactory)
{
    public const string RunningStatus = "Running";
    public const string SucceededStatus = "Succeeded";
    public const string FailedStatus = "Failed";
    public const string SkippedStatus = "Skipped";

    private static readonly string[] CompleteStatuses = [SucceededStatus, FailedStatus, SkippedStatus];

    private readonly IDbContextFactory<LeagueApiDbContext> _dbContextFactory = dbContextFactory;

    public async Task<LeagueRunnerRunEntity> StartRunAsync(string mode, int? season, int? week, string? phase, CancellationToken cancellationToken)
    {
        var normalizedMode = NormalizeRequired(mode, nameof(mode), 32);

        var run = new LeagueRunnerRunEntity
        {
            RunnerRunId = Guid.NewGuid(),
            Mode = normalizedMode,
            Status = RunningStatus,
            Season = season,
            Week = week,
            Phase = NormalizeOptional(phase, 32),
            StartedAtUtc = DateTimeOffset.UtcNow
        };

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        dbContext.LeagueRunnerRuns.Add(run);
        await dbContext.SaveChangesAsync(cancellationToken);
        return run;
    }

    public async Task<LeagueRunnerRunEntity?> RecordAgentAsync(Guid runnerRunId, string agentId, bool succeeded, string? errorMessage, CancellationToken cancellationToken)
    {
        var normalizedAgentId = NormalizeRequired(agentId, nameof(agentId), 100);

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var run = await dbContext.LeagueRunnerRuns.FirstOrDefaultAsync(item => item.RunnerRunId == runnerRunId, cancellationToken);
        if (run is null)
            return null;

        var existing = await dbContext.LeagueRunnerAgentResults.FirstOrDefaultAsync(item => item.RunnerRunId == runnerRunId && item.AgentId == normalizedAgentId, cancellationToken);
        if (existing is null)
        {
            dbContext.LeagueRunnerAgentResults.Add(new LeagueRunnerAgentResultEntity
            {
                RunnerRunId = runnerRunId,
                AgentId = normalizedAgentId,
                Succeeded = succeeded,
                ErrorMessage = NormalizeOptional(errorMessage),
                RecordedAtUtc = DateTimeOffset.UtcNow
            });
        }
        else
        {
            existing.Succeeded = succeeded;
            existing.ErrorMessage = NormalizeOptional(errorMessage);
            existing.RecordedAtUtc = DateTimeOffset.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetRunAsync(dbContext, runnerRunId, cancellationToken);
    }

    public async Task<LeagueRunnerRunEntity?> CompleteRunAsync(Guid runnerRunId, string status, string? summary, string? errorMessage, CancellationToken cancellationToken)
    {
        var normalizedStatus = NormalizeRequired(status, nameof(status), 32);
        if (!CompleteStatuses.Contains(normalizedStatus, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException($"Unsupported status '{status}'. Supported statuses: {string.Join(", ", CompleteStatuses)}.", nameof(status));

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var run = await dbContext.LeagueRunnerRuns.FirstOrDefaultAsync(item => item.RunnerRunId == runnerRunId, cancellationToken);
        if (run is null)
            return null;

        run.Status = CompleteStatuses.First(item => item.Equals(normalizedStatus, StringComparison.OrdinalIgnoreCase));
        run.Summary = NormalizeOptional(summary);
        run.ErrorMessage = NormalizeOptional(errorMessage);
        run.CompletedAtUtc = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetRunAsync(dbContext, runnerRunId, cancellationToken);
    }

    public async Task<LeagueRunnerRunEntity?> GetLatestRunAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var latestRunId = await dbContext.LeagueRunnerRuns.AsNoTracking().OrderByDescending(run => run.StartedAtUtc).Select(run => (Guid?)run.RunnerRunId).FirstOrDefaultAsync(cancellationToken);
        return latestRunId is null ? null : await GetRunAsync(dbContext, latestRunId.Value, cancellationToken);
    }

    public async Task<IReadOnlyList<LeagueRunnerRunEntity>> GetRunsAsync(int limit, CancellationToken cancellationToken)
    {
        var normalizedLimit = Math.Clamp(limit, 1, 100);
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var runIds = await dbContext.LeagueRunnerRuns.AsNoTracking().OrderByDescending(run => run.StartedAtUtc).Take(normalizedLimit).Select(run => run.RunnerRunId).ToListAsync(cancellationToken);
        if (runIds.Count == 0)
            return [];

        var runs = await dbContext.LeagueRunnerRuns.AsNoTracking().Include(run => run.Agents).Where(run => runIds.Contains(run.RunnerRunId)).ToListAsync(cancellationToken);
        var order = runIds.Select((runId, index) => (runId, index)).ToDictionary(item => item.runId, item => item.index);
        return runs.OrderBy(run => order[run.RunnerRunId]).Select(OrderAgents).ToList();
    }

    private static async Task<LeagueRunnerRunEntity?> GetRunAsync(LeagueApiDbContext dbContext, Guid runnerRunId, CancellationToken cancellationToken)
    {
        var run = await dbContext.LeagueRunnerRuns.AsNoTracking().Include(item => item.Agents).FirstOrDefaultAsync(item => item.RunnerRunId == runnerRunId, cancellationToken);
        return run is null ? null : OrderAgents(run);
    }

    private static LeagueRunnerRunEntity OrderAgents(LeagueRunnerRunEntity run)
    {
        run.Agents = run.Agents.OrderBy(agent => agent.RecordedAtUtc).ThenBy(agent => agent.AgentId, StringComparer.Ordinal).ToList();
        return run;
    }

    private static string NormalizeRequired(string value, string name, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{name} is required.", name);

        var normalized = value.Trim();
        if (normalized.Length > maxLength)
            throw new ArgumentException($"{name} must be {maxLength} characters or fewer.", name);

        return normalized;
    }

    private static string? NormalizeOptional(string? value, int? maxLength = null)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = value.Trim();
        if (maxLength is not null && normalized.Length > maxLength.Value)
            throw new ArgumentException($"Value must be {maxLength.Value} characters or fewer.");

        return normalized;
    }
}
