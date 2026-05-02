using System.ClientModel;
using System.Net.Http.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Logging;

enum DraftPickResult { Success, Skipped, Failed }

public class DraftRunner
{
    private static readonly HttpClient _http = new() { BaseAddress = new Uri("http://localhost:5000/") };
    private static readonly System.Text.Json.JsonSerializerOptions _jsonIndented = new() { WriteIndented = true };
    private readonly List<FantasyAgent> _agents;
    private readonly ILogger _logger;
    const int maxDraftPickAttempts = 3;
    const int totalRounds = 2;
    private DraftState _draftState = new();

    // We start by creating a DraftRunner class that will manage the state of the draft
    // and orchestrate the drafting process. It will keep track of the draft state,
    // including which round and pick we're on, and the order of agents in the draft.
    // It will also handle saving and loading this state to a file so that we can resume if needed.
    public DraftRunner(List<FantasyAgent> agents, ILogger logger)
    {
        _agents = agents.ToList();
        _logger = logger;
    }

    public async Task RunDraftAsync()
    {
        // First we check to see if we have a draft-state.json file.
        // This file will have information about the current state of the draft,
        // including which round we're in, which pick we're on, and the fixed draft order.

        _draftState = await GetDraftStateAsync(_agents);
        // Is the draft already complete? If so, we can exit the draft runner.
        if (_draftState.IsDraftComplete)
        {
            _logger.LogInformation("Draft is already complete according to draft-state.json. Exiting draft runner.");
            return;
        }
        if (_draftState.Round == 1 && _draftState.Pick == 1)
        {
            _logger.LogInformation("Starting new draft. Good luck to all the agents!");
        }
        else
        {
            _logger.LogInformation($"Resuming draft from saved state: Round {_draftState.Round}, Pick {_draftState.Pick}");
        }

        // Okay, now that we have the draft set up, lets do it!
        var orderedAgents = GetOrderedAgents();

        // Verify the state is correct.
        int expectedRound = ((_draftState.Pick - 1) / orderedAgents.Count) + 1;
        int picksThisRound = (_draftState.Pick - 1) - ((_draftState.Round - 1) * orderedAgents.Count);

        if (expectedRound != _draftState.Round || picksThisRound < 0 || picksThisRound > orderedAgents.Count)
        {
            throw new InvalidOperationException($"Draft state is inconsistent: Round={_draftState.Round}, Pick={_draftState.Pick}. Delete draft-state.json to restart fresh.");
        }

        // If we've gotten here, we're good!
        // So now, let's loop through every round, giving each agent their chance to pick.
        // There are 15 rounds, so each player can fill their roster.
        // Right now, for testing purposes, we're just doing 2 rounds, but we can increase this to 15 later.
        for(; _draftState.Round <= totalRounds; _draftState.Round++)
        {
            _logger.LogInformation($"Starting round {_draftState.Round}");

            // This is for a snake draft, so the order reverses every other round.
            // In odd rounds, we go in the order of the agents list. In even rounds, we go in reverse order.
            var agentsThisRound = _draftState.Round % 2 == 1 ? orderedAgents : orderedAgents.AsEnumerable().Reverse();
            
            // On resume, skip agents already picked this round
            int picksAlreadyMadeThisRound = (_draftState.Pick - 1) - ((_draftState.Round - 1) * orderedAgents.Count);
            var remainingAgents = agentsThisRound.Skip(picksAlreadyMadeThisRound);

            foreach(var agent in remainingAgents)
            {
                // Draft the player, and then increment the pick number and save the draft state after each pick
                // so we can resume if needed.
                var agentName = agent.GetAgentName()!;
                int pickInRound = (_draftState.Pick - 1) % orderedAgents.Count + 1;
                var rosterCountBefore = await GetAgentRosterCountAsync(agentName);
                _logger.LogInformation("Agent {AgentName} is making pick {PickInRound} in round {Round} (overall pick {Pick}) with roster count {RosterCountBefore}", agentName, pickInRound, _draftState.Round, _draftState.Pick, rosterCountBefore);
                DraftPickResult result = await DraftPlayerAsync(agent, _draftState.Round, _draftState.Pick, maxDraftPickAttempts);
                var rosterCountAfter = await GetAgentRosterCountAsync(agentName);

                if (rosterCountAfter <= rosterCountBefore)
                {
                    _logger.LogWarning("Agent {AgentName} pick at Round={Round} Pick={Pick} did not result in a roster addition. Retrying...", agentName, _draftState.Round, _draftState.Pick);
                    result = await DraftPlayerAsync(agent, _draftState.Round, _draftState.Pick, maxDraftPickAttempts);
                    rosterCountAfter = await GetAgentRosterCountAsync(agentName);
                    if (rosterCountAfter <= rosterCountBefore)
                    {
                        _logger.LogWarning("Agent {AgentName} pick at Round={Round} Pick={Pick} still did not result in a roster addition after retry. Skipping.", agentName, _draftState.Round, _draftState.Pick);
                        result = DraftPickResult.Skipped;
                    }
                }

                _logger.LogInformation("Pick complete — Round={Round} Pick={Pick} PickInRound={PickInRound} Agent={Agent} Result={Result}", _draftState.Round, _draftState.Pick, pickInRound, agentName, result);
                _draftState.Pick++;
                await SaveDraftStateAsync();
            }
        }

        // Draft is complete! Update state and save.
        _draftState.IsDraftComplete = true;
        await SaveDraftStateAsync(writeIndented: true);
        _logger.LogInformation("Draft is complete!");
    }

    // This is a simple helper method to save the draft state to a file.
    // We call this after every pick and round so that we can resume if needed.
    async Task SaveDraftStateAsync(bool writeIndented = false)
    {
        var options = writeIndented ? _jsonIndented : System.Text.Json.JsonSerializerOptions.Default;
        var draftStateJson = System.Text.Json.JsonSerializer.Serialize(_draftState, options);
        await File.WriteAllTextAsync("draft-state.json", draftStateJson);
    }

    // This is another helper method to get the list of agents in the order of the draft based on the draft state.
    // This might need some work we have to test it a little bit.
    List<FantasyAgent> GetOrderedAgents()
    {
        if (_draftState.DraftOrder.Count == 0)
        {
            return _agents.ToList();
        }

        var agentsByName = _agents
            .Where(agent => !string.IsNullOrWhiteSpace(agent.GetAgentName()))
            .ToDictionary(agent => agent.GetAgentName(), StringComparer.OrdinalIgnoreCase);

        return _draftState.DraftOrder.Select(agentName =>
        {
            if (!agentsByName.TryGetValue(agentName, out var agent))
            {
                throw new InvalidOperationException($"Draft order references unknown agent '{agentName}'.");
            }

            return agent;
        }).ToList();
    }

    // This where the real work is to draft a player.
    // We give the agent a prompt with the current round and pick, and ask them to use their tools to research and add a player to their roster.
    // This has the added benefit of a retry/backoff mechanism in case something goes wrong with the agent's response or tool use, which can happen sometimes!
    async Task<DraftPickResult> DraftPlayerAsync(FantasyAgent agent, int round, int pick, int maxAttempts)
    {
        // The main prompt being passed to the agent for drafting.
        var draftPrompt = $"""
            The leauge is drafting, and you're up to select a player! One player only.
            This is currently round {round} of 15 total rounds, and this is pick {pick} of 150 total picks.
            Look at your roster and identify what player you need to draft next.
            Call `GetAvailablePlayers` filtered by the needed position to see players who are still available.
            Use the `SearchWeb` tool to research the available players, looking at their stats, news, and any other relevant information to make an informed decision.
            The `AddPlayerToRoster` tool allows you to add a player to your roster once you've made your decision.
            If the `AddPlayerToRoster` tool fails, you will need to find a different player to add to your roster, so you should go back to researching and find another player to add.
            The `WriteAgentBootstrap` tool allows you to update your bootstrap file, so you can use that to keep track of your strategy and notes about the players you're drafting if you'd like.
            Respond with the name of the player you added and why you chose that player based on your strategy and team needs.

        """;

        // Sometimes things go slow, the agent might not respond in time, or there might be transient errors.
        var draftPickRetryBackoffs = new[] { TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(90) };
        
        // Make sure the agent has a name for logging purposes, and then attempt to make the draft pick with retries and backoff in case of failure.
        var agentName = agent.GetAgentName() ?? throw new InvalidOperationException("Draft agent name is required before making picks.");

        // So we do three attempts to make the draft pick. If it fails due to a timeout, transient error, or other issue, we catch that and retry after a delay.
        // If it continues to fail after the max attempts, we log that and skip the pick so the draft can continue.
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var response = await agent.RunAsync(draftPrompt);
                _logger.LogInformation("Agent {agentName} response: {Response}", agentName, response.Text);
                await LogDecisionAsync(agentName, 0, "Add Player", response, "Draft", _logger);
                return DraftPickResult.Success;
            }
            catch (Exception ex) when (IsDraftPickFailure(ex) && attempt < maxAttempts)
            {
                var retryBackoff = draftPickRetryBackoffs[Math.Min(attempt - 1, draftPickRetryBackoffs.Length - 1)];
                _logger.LogWarning(ex, "Draft pick {Pick} in round {Round} for agent {AgentName} failed on attempt {Attempt}/{MaxAttempts}; retrying in {RetryDelaySeconds} seconds.", pick, round, agentName, attempt, maxAttempts, retryBackoff.TotalSeconds);

                await Task.Delay(retryBackoff);
            }
            catch (Exception ex) when (IsDraftPickFailure(ex))
            {
                _logger.LogWarning(ex, "Draft pick {Pick} in round {Round} for agent {AgentName} failed after {MaxAttempts} attempts; skipping this pick.", pick, round, agentName, maxAttempts);
                return DraftPickResult.Failed;
            }
        }
        return DraftPickResult.Skipped;
    }

    // This is a helper method to determine if an exception that occurred during the draft pick process is something we
    // want to retry on (like a timeout or transient error) or if it's something else.
    // It's an awkward method, but it helps keep the retry logic cleaner by abstracting out the exception handling.
    static bool IsDraftPickFailure(Exception ex)
    {
        return ex is TaskCanceledException
            || ex is TimeoutException
            || ex is HttpRequestException
            || ex is IOException
            || IsTransientClientResultException(ex)
            || ex is ArgumentOutOfRangeException { Message: var message }
                && message.Contains("ChatFinishReason", StringComparison.OrdinalIgnoreCase);
    }

    // This is another helper method to determine if an exception is a transient error from the client that we might 
    // want to retry on. Might be worth expanding this in the future to include more specific handling based on the API and client being used.
    static bool IsTransientClientResultException(Exception ex)
    {
        return ex is ClientResultException { Status: 408 or 409 or 429 or >= 500 };
    }

    async Task<int> GetAgentRosterCountAsync(string agentId)
    {
        var roster = await _http.GetFromJsonAsync<List<object>>($"/api/rosters/{agentId}");
        return roster?.Count ?? 0;
    }

    public async Task<DraftState> GetDraftStateAsync(List<FantasyAgent> fantasyAgents)
    {
        // First we check to see if we have a draft-state.json file.
        // This file will have information about the current state of the draft,
        // including which round we're in, which pick we're on, and the fixed draft order.
        var draftState = new DraftState();
        if (File.Exists("draft-state.json"))
        {
            var draftStateJson = await File.ReadAllTextAsync("draft-state.json");
            draftState = System.Text.Json.JsonSerializer.Deserialize<DraftState>(draftStateJson, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return draftState ?? new DraftState();
        }
        else
        {
            // If there is no draft-state.json file, we can create one to track the state of the draft as it progresses.
            // The draft order is determined here (randomized) and persisted so it stays consistent across runs/resumes.
            _logger.LogInformation("No existing draft state found. Starting new draft.");

            // Here we create a random order for the agents!
            var randomizedAgents = fantasyAgents.OrderBy(a => Guid.NewGuid()).ToList();

            // Now create a new Draft state.
            var initialDraftState = new DraftState
            {
                IsDraftComplete = false,
                Round = 1,
                Pick = 1,
                DraftOrder = randomizedAgents.Select(a => a.GetAgentName() ?? "unknown").ToList()
            };

            // Save the draft state so we can begin.
            draftState = initialDraftState;
            await SaveDraftStateAsync(writeIndented: true);
            _logger.LogInformation("Saved initial draft order: {Order}", string.Join(" -> ", draftState.DraftOrder));
            return draftState;
        }
    }

    // This is a helper method for the agents to log the decisoons they make during the draft.
    // This can be useful for tracking the agents' reasoning and actions, and for debugging if needed.
    static async Task LogDecisionAsync(string agentId, int week, string type, AgentResponse response, string action, ILogger logger)
    {
        var usage = response.Usage;
        var payload = new
        {
            agentId,
            week,
            type,
            reasoning = response.Text,
            action,
            inputTokenCount = (int?)usage?.InputTokenCount,
            outputTokenCount = (int?)usage?.OutputTokenCount,
            cachedInputTokenCount = (int?)usage?.CachedInputTokenCount,
            reasoningTokenCount = (int?)usage?.ReasoningTokenCount
        };

        try
        {
            var decisionResponse = await _http.PostAsJsonAsync("/api/decisions", payload);
            decisionResponse.EnsureSuccessStatusCode();
            logger.LogInformation("Logged decision for {AgentId}: {Type} - {Action}", agentId, type, action);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to log decision for {AgentId}", agentId);
        }
    }
}
