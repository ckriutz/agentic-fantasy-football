using System.ComponentModel;
using System.Text.Json;
using System.Text.RegularExpressions;
using AgenticLeague.Models;

public sealed class AgentProfileTools
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _rootPath;

    public AgentProfileTools(string? rootPath = null)
    {
        _rootPath = rootPath ?? Directory.GetCurrentDirectory();
    }

    [Description("Reads the agent's profile.json file. Returns a default profile shape if one does not exist yet.")]
    public async Task<AgentProfile> ReadAgentProfile(
        [Description("The agent ID, such as player-01.")] string agentId)
    {
        var safeAgentId = GetSafeAgentId(agentId);
        var profilePath = GetProfilePath(safeAgentId);

        if (!File.Exists(profilePath))
        {
            return CreateDefaultProfile(safeAgentId, modelName: string.Empty);
        }

        var json = await File.ReadAllTextAsync(profilePath);
        var profile = JsonSerializer.Deserialize<AgentProfile>(json);

        if (profile is null)
        {
            throw new InvalidOperationException($"Profile file '{profilePath}' could not be deserialized.");
        }

        profile.AgentId = safeAgentId;
        profile.BootstrapPath = GetBootstrapPath(safeAgentId);
        return profile;
    }

    [Description("Creates profile.json if it does not already exist.")]
    public async Task<AgentProfile> InitializeAgentProfile(
        [Description("The agent ID, such as player-01.")] string agentId,
        [Description("The OpenRouter model name backing the agent.")] string modelName)
    {
        if (string.IsNullOrWhiteSpace(modelName))
        {
            throw new ArgumentException("Model name is required.", nameof(modelName));
        }

        var safeAgentId = GetSafeAgentId(agentId);
        var profilePath = GetProfilePath(safeAgentId);

        if (File.Exists(profilePath))
        {
            return await ReadAgentProfile(safeAgentId);
        }

        var profile = CreateDefaultProfile(safeAgentId, modelName.Trim());
        await SaveProfile(profile);
        return profile;
    }

    [Description("Updates the agent's team name in profile.json.")]
    public async Task<AgentProfile> SetTeamName(
        [Description("The agent ID, such as player-01.")] string agentId,
        [Description("The team name chosen by the agent.")] string teamName)
    {
        if (string.IsNullOrWhiteSpace(teamName))
        {
            throw new ArgumentException("Team name is required.", nameof(teamName));
        }

        var profile = await LoadOrCreateProfile(agentId);
        profile.TeamName = teamName.Trim();
        profile.LastUpdatedAt = DateTime.UtcNow;

        await SaveProfile(profile);
        return profile;
    }

    [Description("Updates whether the agent has completed bootstrap.")]
    public async Task<AgentProfile> SetBootstrapStatus(
        [Description("The agent ID, such as player-01.")] string agentId,
        [Description("True when bootstrap.md has been created and accepted.")] bool isBootstrapped)
    {
        var profile = await LoadOrCreateProfile(agentId);
        profile.IsBootstrapped = isBootstrapped;
        profile.LastUpdatedAt = DateTime.UtcNow;

        await SaveProfile(profile);
        return profile;
    }

    [Description("Updates the relative or absolute logo path stored in profile.json.")]
    public async Task<AgentProfile> SetLogoPath(
        [Description("The agent ID, such as player-01.")] string agentId,
        [Description("The relative or absolute logo file path. Use an empty string to clear it.")] string? logoPath)
    {
        var profile = await LoadOrCreateProfile(agentId);
        profile.LogoPath = string.IsNullOrWhiteSpace(logoPath) ? null : logoPath.Trim();
        profile.LastUpdatedAt = DateTime.UtcNow;

        await SaveProfile(profile);
        return profile;
    }

    private async Task<AgentProfile> LoadOrCreateProfile(string agentId)
    {
        var safeAgentId = GetSafeAgentId(agentId);
        var profilePath = GetProfilePath(safeAgentId);

        if (File.Exists(profilePath))
        {
            return await ReadAgentProfile(safeAgentId);
        }

        var profile = CreateDefaultProfile(safeAgentId, modelName: string.Empty);
        await SaveProfile(profile);
        return profile;
    }

    private async Task SaveProfile(AgentProfile profile)
    {
        Directory.CreateDirectory(GetAgentFolder(profile.AgentId));

        profile.AgentId = GetSafeAgentId(profile.AgentId);
        profile.BootstrapPath = GetBootstrapPath(profile.AgentId);
        if (profile.CreatedAtUtc == default)
        {
            profile.CreatedAtUtc = DateTime.UtcNow;
        }

        if (profile.LastUpdatedAt == default)
        {
            profile.LastUpdatedAt = profile.CreatedAtUtc;
        }

        var profilePath = GetProfilePath(profile.AgentId);
        var json = JsonSerializer.Serialize(profile, JsonOptions);
        await File.WriteAllTextAsync(profilePath, json);
    }

    private AgentProfile CreateDefaultProfile(string safeAgentId, string modelName)
    {
        var now = DateTime.UtcNow;
        return new AgentProfile
        {
            AgentId = safeAgentId,
            ModelName = modelName,
            BootstrapPath = GetBootstrapPath(safeAgentId),
            CreatedAtUtc = now,
            LastUpdatedAt = now,
            IsBootstrapped = false
        };
    }

    private string GetAgentFolder(string safeAgentId)
    {
        return Path.Combine(_rootPath, "agents", safeAgentId);
    }

    private string GetProfilePath(string safeAgentId)
    {
        return Path.Combine(GetAgentFolder(safeAgentId), "profile.json");
    }

    private string GetBootstrapPath(string safeAgentId)
    {
        return Path.Combine(GetAgentFolder(safeAgentId), "bootstrap.md");
    }

    private static string GetSafeAgentId(string agentId)
    {
        if (string.IsNullOrWhiteSpace(agentId))
        {
            throw new ArgumentException("Agent ID is required.", nameof(agentId));
        }

        var safeAgentId = Regex.Replace(agentId.Trim(), @"[^a-zA-Z0-9\-_]", "");
        if (string.IsNullOrWhiteSpace(safeAgentId))
        {
            throw new InvalidOperationException("Agent ID must contain at least one valid character.");
        }

        return safeAgentId;
    }
}
