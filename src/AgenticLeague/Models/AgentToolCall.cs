namespace AgenticLeague.Models;

// Captures a single tool/function invocation made during one agent run.
// This is an in-memory diagnostic record only - nothing here is persisted to storage.
public class AgentToolCall
{
    // Correlates this call back to the single RunAsync execution it belongs to.
    public string RunId { get; set; } = string.Empty;

    // Which agent (e.g. player-07) made the call.
    public string AgentId { get; set; } = string.Empty;

    // 1-based order in which this call happened within the run.
    public int Sequence { get; set; }

    // The tool/function name (e.g. add_free_agent_for_current_week).
    public string ToolName { get; set; } = string.Empty;

    // A readable "name=value, name=value" summary of the arguments passed in.
    public string Arguments { get; set; } = string.Empty;

    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset CompletedAtUtc { get; set; }

    // Whether the local invocation pipeline returned or threw. See the note on the enum below.
    public AgentToolCallStatus Status { get; set; } = AgentToolCallStatus.Completed;

    // A decoded, size-bounded view of whatever the tool returned.
    public string ResultSummary { get; set; } = string.Empty;

    // Populated only when the invocation threw an exception.
    public string? ErrorMessage { get; set; }

    // How long the invocation took, derived from the start/complete timestamps.
    public double DurationMs => (CompletedAtUtc - StartedAtUtc).TotalMilliseconds;
}

// The outcome of the local tool invocation pipeline.
// IMPORTANT: Completed only means the call returned without throwing. It does NOT prove that a
// roster/transaction mutation actually succeeded - a tool can return an MCP payload with
// isError=true and still be recorded as Completed here.
public enum AgentToolCallStatus
{
    Completed,
    Failed
}
