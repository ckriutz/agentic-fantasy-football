using Microsoft.Agents.AI;

namespace AgenticLeague.Models;

// Wraps everything produced by one FantasyAgent.RunAsync call:
// the model's final response plus the ordered list of tool calls it made along the way.
// Callers that only care about the text can read .Response; the tool trace is there for diagnostics.
public class AgentRunResult
{
    // Unique id for this run, shared with every AgentToolCall it produced.
    public string RunId { get; set; } = string.Empty;

    // The agent's final response (may have empty Text if the model ended on a tool call).
    public AgentResponse Response { get; set; } = new();

    // Every tool call captured during the run, in the order they happened.
    public IReadOnlyList<AgentToolCall> ToolCalls { get; set; } = new List<AgentToolCall>();
}
