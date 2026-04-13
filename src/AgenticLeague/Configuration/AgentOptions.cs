namespace AgenticLeague.Configuration;

/// <summary>
/// Root configuration for all agents. Maps to appsettings.json "Agents" section.
/// </summary>
public class AgentOptions
{
    public string? OpenRouterApiKey { get; set; }
    public List<AgentDefinition> Definitions { get; set; } = new();
}
