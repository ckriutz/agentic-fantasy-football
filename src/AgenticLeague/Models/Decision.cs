namespace AgenticLeague.Models;

/// <summary>
/// A single agent decision event.
/// Appended to decisions.jsonl in the agent's workspace.
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
    /// Which NFL week this decision pertains to.
    /// </summary>
    public int Week { get; set; }

    /// <summary>
    /// Decision category (e.g., "draft_pick", "waiver_claim", "bench_swap", "start_sit").
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Context snapshot (e.g., available players, current roster, scores).
    /// </summary>
    public string Context { get; set; } = string.Empty;

    /// <summary>
    /// Agent's stated reasoning for the decision.
    /// </summary>
    public string Reasoning { get; set; } = string.Empty;

    /// <summary>
    /// The action taken (e.g., "Drafted player X (sleeper_id: 1234)").
    /// </summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// Outcome (null until week completes; e.g., "scored 18.5 points").
    /// </summary>
    public string? Outcome { get; set; }
}
