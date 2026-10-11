namespace LeagueAPI.Models;

public sealed class LeagueRunnerRunEntity
{
    public Guid RunnerRunId { get; set; }

    public required string Mode { get; set; }

    public required string Status { get; set; }

    public int? Season { get; set; }

    public int? Week { get; set; }

    public string? Phase { get; set; }

    public string? Summary { get; set; }

    public string? ErrorMessage { get; set; }

    public DateTimeOffset StartedAtUtc { get; set; }

    public DateTimeOffset? CompletedAtUtc { get; set; }

    public List<LeagueRunnerAgentResultEntity> Agents { get; set; } = [];
}

public sealed class LeagueRunnerAgentResultEntity
{
    public int LeagueRunnerAgentResultId { get; set; }

    public Guid RunnerRunId { get; set; }

    public required string AgentId { get; set; }

    public bool Succeeded { get; set; }

    public string? ErrorMessage { get; set; }

    public DateTimeOffset RecordedAtUtc { get; set; }
}
