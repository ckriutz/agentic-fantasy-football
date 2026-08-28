using Microsoft.Extensions.Logging;

public sealed class TestRunner(List<FantasyAgent> agents, ILogger<TestRunner> logger)
{
    private readonly List<FantasyAgent> _agents = agents;
    private readonly ILogger<TestRunner> _logger = logger;

    public async Task RunAsync()
    {
        if (_agents.Count == 0)
            throw new InvalidOperationException("The skill smoke test requires at least one enabled agent.");

        _logger.LogInformation("Running the skill smoke test for {AgentCount} enabled agents.", _agents.Count);

        foreach (var agent in _agents)
        {
            var agentId = agent.GetAgentName();
            var result = await agent.RunAsync("Use the `skill-smoke-test` skill to run the skills verification test. Do not use any other skill.");
            var response = result.Response.Text?.Trim();
            _logger.LogInformation("Agent {AgentId} produced response: {Response}", agentId, response);
        }

        _logger.LogInformation("Test completed for all {AgentCount} enabled agents.", _agents.Count);
    }
}
