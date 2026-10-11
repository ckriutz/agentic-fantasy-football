namespace LeagueAPI.Models;

public sealed record StartLeagueRunnerRunRequest(string Mode, int? Season, int? Week, string? Phase);

public sealed record RecordLeagueRunnerAgentRequest(string AgentId, bool Succeeded, string? ErrorMessage);

public sealed record CompleteLeagueRunnerRunRequest(string Status, string? Summary, string? ErrorMessage);
