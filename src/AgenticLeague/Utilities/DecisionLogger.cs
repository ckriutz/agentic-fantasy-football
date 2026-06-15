using System.Net.Http.Json;
using AgenticLeague.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Logging;

internal static class DecisionLogger
{
    private static readonly HttpClient _http = new() { BaseAddress = new Uri("http://localhost:5000/") };

    internal static Task LogDecisionAsync(string agentId, int week, string type, AgentResponse response, string action, ILogger logger)
    {
        var usage = response.Usage;
        var decision = new Decision
        {
            AgentId = agentId,
            Week = week,
            Type = type,
            Reasoning = response.Text,
            Action = action,
            InputTokenCount = (int?)usage?.InputTokenCount,
            OutputTokenCount = (int?)usage?.OutputTokenCount,
            CachedInputTokenCount = (int?)usage?.CachedInputTokenCount,
            ReasoningTokenCount = (int?)usage?.ReasoningTokenCount
        };

        return LogDecisionAsync(decision, logger);
    }

    internal static Task LogDecisionAsync(string agentId, int week, string type, string response, string action, ILogger logger)
    {
        var decision = new Decision
        {
            AgentId = agentId,
            Week = week,
            Type = type,
            Reasoning = response,
            Action = action,
            InputTokenCount = 0,
            OutputTokenCount = 0,
            CachedInputTokenCount = 0,
            ReasoningTokenCount = 0
        };

        return LogDecisionAsync(decision, logger);
    }

    private static async Task LogDecisionAsync(Decision decision, ILogger logger)
    {
        try
        {
            var decisionResponse = await _http.PostAsJsonAsync("/api/decisions", decision);
            decisionResponse.EnsureSuccessStatusCode();
            logger.LogInformation("Logged decision for {AgentId}: {Type} - {Action}", decision.AgentId, decision.Type, decision.Action);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to log decision for {AgentId}", decision.AgentId);
        }
    }
}
