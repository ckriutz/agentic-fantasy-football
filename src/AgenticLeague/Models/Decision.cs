namespace AgenticLeague.Models;

/// <summary>
/// A single agent decision event, persisted to Postgres via LeagueAPI.
/// </summary>
public class Decision
{
    /// <summary>
    /// When the decision was made.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Which agent made this decision.
    /// </summary>
    public string AgentId { get; set; } = string.Empty;

    /// <summary>
    /// Which NFL week this decision pertains to (0 for pre-season/draft).
    /// </summary>
    public int Week { get; set; }

    /// <summary>
    /// Decision category (e.g., "draft_pick", "roster_add", "roster_drop", "bench_swap", "start_sit").
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Agent's stated reasoning for the decision.
    /// </summary>
    public string Reasoning { get; set; } = string.Empty;

    /// <summary>
    /// The action taken (e.g., "Drafted player X (sleeper_id: 1234)").
    /// </summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// Number of input tokens consumed by the LLM call.
    /// </summary>
    public int? InputTokenCount { get; set; }

    /// <summary>
    /// Number of output tokens produced by the LLM call.
    /// </summary>
    public int? OutputTokenCount { get; set; }

    /// <summary>
    /// Number of cached input tokens (prompt-cache hits).
    /// </summary>
    public int? CachedInputTokenCount { get; set; }

    /// <summary>
    /// Number of reasoning/thinking tokens used by the LLM call.
    /// </summary>
    public int? ReasoningTokenCount { get; set; }
}
