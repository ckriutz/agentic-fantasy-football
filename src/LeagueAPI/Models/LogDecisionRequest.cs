namespace LeagueAPI.Models;

public sealed record LogDecisionRequest(
    string AgentId,
    int Week,
    string Type,
    string Reasoning,
    string Action);
