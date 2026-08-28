using System.Text.RegularExpressions;
using LeagueAPI.Data;
using LeagueAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace LeagueAPI.Services;

public sealed class AgentProfileStore(IDbContextFactory<LeagueApiDbContext> dbContextFactory) : IAgentProfileReader, IAgentProfileWriter
{
    private const int MaxAgentIdLength = 100;
    private const int MaxConnectionLength = 50;
    private const int MaxModelNameLength = 200;
    private const int MaxTeamNameLength = 200;
    private static readonly Regex SafeAgentIdRegex = new(@"^[a-zA-Z0-9\-_]+$", RegexOptions.Compiled);
    private readonly IDbContextFactory<LeagueApiDbContext> _dbContextFactory = dbContextFactory;

    public async Task<IReadOnlyList<AgentProfile>> GetAgentProfilesAsync(bool enabledOnly, CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var query = dbContext.AgentProfiles.AsNoTracking();

        if (enabledOnly)
            query = query.Where(profile => profile.IsEnabled);

        return await query
            .OrderBy(profile => profile.AgentId)
            .ToListAsync(cancellationToken);
    }

    public async Task<AgentProfile?> GetAgentProfileAsync(string agentId, CancellationToken cancellationToken)
    {
        var normalizedAgentId = NormalizeAgentId(agentId);

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await dbContext.AgentProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(profile => profile.AgentId == normalizedAgentId, cancellationToken);
    }

    public async Task<AgentProfile> UpsertAgentProfileAsync(string agentId, string modelName, string connection, string? teamName, bool? isBootstrapped, bool? isEnabled, CancellationToken cancellationToken)
    {
        var normalizedAgentId = NormalizeAgentId(agentId);
        var normalizedModelName = NormalizeRequired(modelName, nameof(modelName), MaxModelNameLength);
        var normalizedConnection = NormalizeRequired(connection, nameof(connection), MaxConnectionLength);
        var normalizedTeamName = NormalizeOptional(teamName, nameof(teamName), MaxTeamNameLength);
        var now = DateTimeOffset.UtcNow;

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var profile = await dbContext.AgentProfiles
            .FirstOrDefaultAsync(row => row.AgentId == normalizedAgentId, cancellationToken);

        if (profile is null)
        {
            profile = new AgentProfile
            {
                AgentId = normalizedAgentId,
                ModelName = normalizedModelName,
                Connection = normalizedConnection,
                TeamName = normalizedTeamName ?? string.Empty,
                IsBootstrapped = isBootstrapped ?? false,
                IsEnabled = isEnabled ?? true,
                CreatedAtUtc = now,
                LastUpdatedAt = now
            };

            dbContext.AgentProfiles.Add(profile);
        }
        else
        {
            profile.ModelName = normalizedModelName;
            profile.Connection = normalizedConnection;
            if (normalizedTeamName is not null)
                profile.TeamName = normalizedTeamName;
            if (isBootstrapped.HasValue)
                profile.IsBootstrapped = isBootstrapped.Value;
            if (isEnabled.HasValue)
                profile.IsEnabled = isEnabled.Value;
            profile.LastUpdatedAt = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return profile;
    }

    public async Task<AgentProfile> SetTeamNameAsync(string agentId, string teamName, CancellationToken cancellationToken)
    {
        var normalizedAgentId = NormalizeAgentId(agentId);
        var normalizedTeamName = NormalizeRequired(teamName, nameof(teamName), MaxTeamNameLength);
        var now = DateTimeOffset.UtcNow;

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var profile = await GetOrCreatePlaceholderProfileAsync(dbContext, normalizedAgentId, now, cancellationToken);

        profile.TeamName = normalizedTeamName;
        profile.LastUpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);
        return profile;
    }

    public async Task<AgentProfile> SetBootstrapStatusAsync(string agentId, bool isBootstrapped, CancellationToken cancellationToken)
    {
        var normalizedAgentId = NormalizeAgentId(agentId);
        var now = DateTimeOffset.UtcNow;

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var profile = await GetOrCreatePlaceholderProfileAsync(dbContext, normalizedAgentId, now, cancellationToken);

        profile.IsBootstrapped = isBootstrapped;
        profile.LastUpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);
        return profile;
    }

    private static async Task<AgentProfile> GetOrCreatePlaceholderProfileAsync(LeagueApiDbContext dbContext, string agentId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO agent_profiles ("AgentId", "TeamName", "ModelName", "Connection", "CreatedAtUtc", "LastUpdatedAt", "IsBootstrapped", "IsEnabled")
            VALUES ({agentId}, '', '', '', {now}, {now}, FALSE, FALSE)
            ON CONFLICT ("AgentId") DO NOTHING
            """,
            cancellationToken);

        return await dbContext.AgentProfiles
            .SingleAsync(profile => profile.AgentId == agentId, cancellationToken);
    }

    private static string NormalizeAgentId(string agentId)
    {
        if (string.IsNullOrWhiteSpace(agentId))
            throw new ArgumentException("Agent ID is required.", nameof(agentId));

        var normalizedAgentId = agentId.Trim();
        if (normalizedAgentId.Length > MaxAgentIdLength)
            throw new ArgumentException($"Agent ID must be {MaxAgentIdLength} characters or fewer.", nameof(agentId));
        if (!SafeAgentIdRegex.IsMatch(normalizedAgentId))
            throw new ArgumentException("Agent ID can only contain letters, numbers, hyphens, and underscores.", nameof(agentId));

        return normalizedAgentId;
    }

    private static string NormalizeRequired(string value, string paramName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{paramName} is required.", paramName);

        var normalizedValue = value.Trim();
        if (normalizedValue.Length > maxLength)
            throw new ArgumentException($"{paramName} must be {maxLength} characters or fewer.", paramName);

        return normalizedValue;
    }

    private static string? NormalizeOptional(string? value, string paramName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalizedValue = value.Trim();
        if (normalizedValue.Length > maxLength)
            throw new ArgumentException($"{paramName} must be {maxLength} characters or fewer.", paramName);

        return normalizedValue;
    }
}
