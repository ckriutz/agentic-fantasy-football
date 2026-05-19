namespace AgenticLeague.Models;

/// <summary>

/// </summary>
public class AgentConfig
{
    /// <summary>
    /// Unique agent identifier (e.g., "agent-01").
    /// </summary>
    public string AgentName { get; set; } = string.Empty;

    /// <summary>
    /// OpenRouter model to use for this agent (e.g., "x-ai/grok-4.3").
    /// </summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// OpenRouter connection name to use for this agent (e.g., "OpenRouter").
    /// </summary>
    public string Connection { get; set; } = string.Empty;
}