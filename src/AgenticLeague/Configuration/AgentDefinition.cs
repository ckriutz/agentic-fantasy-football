namespace AgenticLeague.Configuration;

/// <summary>
/// Per-agent configuration from appsettings.json.
/// </summary>
public class AgentDefinition
{
    /// <summary>
    /// Unique agent identifier (e.g., "agent-01").
    /// </summary>
    public string AgentId { get; set; } = string.Empty;

    /// <summary>
    /// OpenRouter model identifier (e.g., "anthropic/claude-opus-4.6").
    /// </summary>
    public string ModelName { get; set; } = string.Empty;

    /// <summary>
    /// Strategy hint to seed agent personality (e.g., "aggressive drafter", "analytics-focused").
    /// Used during first-launch strategy generation.
    /// </summary>
    public string PersonaHint { get; set; } = string.Empty;
}
