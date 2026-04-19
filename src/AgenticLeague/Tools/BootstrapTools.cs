using System.ComponentModel;
using System.Text.RegularExpressions;

public sealed class BootstrapTools
{
    private readonly string _rootPath;

    public BootstrapTools(string? rootPath = null)
    {
        _rootPath = rootPath ?? Directory.GetCurrentDirectory();
    }

    [Description("Creates or updates the agent's bootstrap markdown file.")]
    public async Task<string> WriteAgentBootstrap([Description("The agent ID, such as agent-01.")] string agentId, [Description("The markdown content to write.")] string content)
    {
        var safeAgentId = GetSafeAgentId(agentId);
        var bootstrapPath = GetBootstrapPath(safeAgentId);

        Directory.CreateDirectory(Path.GetDirectoryName(bootstrapPath)!);
        await File.WriteAllTextAsync(bootstrapPath, content);

        return $"Bootstrap written to {bootstrapPath}";
    }

    [Description("Reads the agent's bootstrap markdown file.")]
    public async Task<string> ReadAgentBootstrap([Description("The agent ID, such as agent-01.")] string agentId)
    {
        var safeAgentId = GetSafeAgentId(agentId);
        var bootstrapPath = GetBootstrapPath(safeAgentId);

        if (!File.Exists(bootstrapPath))
        {
            return $"Bootstrap file not found for agent '{safeAgentId}'.";
        }

        return await File.ReadAllTextAsync(bootstrapPath);
    }

    private string GetBootstrapPath(string safeAgentId)
    {
        return Path.Combine(_rootPath, "agents", safeAgentId, "bootstrap.md");
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