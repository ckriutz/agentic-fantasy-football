using LeagueAPI.Data;
using LeagueAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace LeagueAPI.Services;

public sealed class PostgresDecisionStore(IDbContextFactory<LeagueApiDbContext> dbContextFactory) : IDecisionReader, IDecisionWriter
{
    private readonly IDbContextFactory<LeagueApiDbContext> _dbContextFactory = dbContextFactory;

    public async Task<DecisionEntity> LogDecisionAsync(
        string agentId,
        int week,
        string type,
        string reasoning,
        string action,
        CancellationToken cancellationToken)
    {
        var normalizedAgentId = NormalizeRequired(agentId, nameof(agentId));
        var normalizedType = NormalizeRequired(type, nameof(type));

        var entity = new DecisionEntity
        {
            AgentId = normalizedAgentId,
            Week = week,
            Type = normalizedType,
            Reasoning = reasoning?.Trim() ?? string.Empty,
            Action = action?.Trim() ?? string.Empty,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        dbContext.Decisions.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return entity;
    }

    public async Task<IReadOnlyList<DecisionEntity>> GetDecisionsByAgentAsync(
        string agentId,
        CancellationToken cancellationToken)
    {
        var normalizedAgentId = NormalizeRequired(agentId, nameof(agentId));

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await dbContext.Decisions
            .AsNoTracking()
            .Where(d => d.AgentId == normalizedAgentId)
            .OrderByDescending(d => d.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DecisionEntity>> GetAllDecisionsAsync(
        string? agentId,
        string? type,
        int? week,
        int limit,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var query = dbContext.Decisions.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(agentId))
            query = query.Where(d => d.AgentId == agentId.Trim());

        if (!string.IsNullOrWhiteSpace(type))
            query = query.Where(d => d.Type == type.Trim());

        if (week.HasValue)
            query = query.Where(d => d.Week == week.Value);

        var normalizedLimit = limit is > 0 and <= 200 ? limit : 50;

        return await query
            .OrderByDescending(d => d.CreatedAtUtc)
            .Take(normalizedLimit)
            .ToListAsync(cancellationToken);
    }

    private static string NormalizeRequired(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{paramName} is required.", paramName);

        return value.Trim();
    }
}
