using System.ClientModel;
using System.ComponentModel.DataAnnotations;
using System.Net.Http.Json;
using System.Runtime.Serialization;
using System.Xml.Schema;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Logging;

public class DraftRunner
{
    private HttpClient _http;
    private static readonly System.Text.Json.JsonSerializerOptions _jsonIndented = new() { WriteIndented = true };
    private readonly List<FantasyAgent> _agents;
    private readonly ILogger _logger;
    const int maxDraftPickAttempts = 3;
    const int maxRosterSize = 16;
    const int totalRounds = 16;
    private DraftState _draftState = new();
    private BlobStorageTools _blobStorageTools;

    // We start by creating a DraftRunner class that will manage the state of the draft
    // and orchestrate the drafting process. It will keep track of the draft state,
    // including which round and pick we're on, and the order of agents in the draft.
    // It will also handle saving and loading this state to a file so that we can resume if needed.
    public DraftRunner(List<FantasyAgent> agents, ILogger logger, HttpClient httpClient)
    {
        _agents = agents.ToList();
        _logger = logger;
        _blobStorageTools = new BlobStorageTools();
        _http = httpClient;
    }

    public async Task<DraftState> RunDraftAsync()
    {
        // First we check to see if we have a draft-state.json file.
        // This file will have information about the current state of the draft,
        // including which round we're in, which pick we're on, and the fixed draft order.

        _draftState = await GetDraftStateAsync(_agents);
        // Is the draft already complete? If so, we can exit the draft runner.
        if (_draftState.IsDraftComplete)
        {
            _logger.LogInformation("Draft is already complete according to draft-state.json. Exiting draft runner.");
            return _draftState;
        }
        if (_draftState.Round == 1 && _draftState.Pick == 1)
        {
            _logger.LogInformation("Starting new draft. Good luck to all the agents!");
        }
        else
        {
            _logger.LogInformation($"Resuming draft from saved state: Round {_draftState.Round}, Pick {_draftState.Pick}");
        }

        // Right now, I only want to run the first round.
        if (_draftState.Round > 1)
        {
            _logger.LogInformation("Only running the first round for now. Exiting.");
            return _draftState;
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
                var agentName = agent.GetAgentName()!;
                int pickInRound = (_draftState.Pick - 1) % orderedAgents.Count + 1;
                var rosterCountBefore = await GetAgentRosterCountAsync(agentName);

                _logger.LogInformation("Agent {AgentName} is making pick {PickInRound} in round {Round} (overall pick {Pick}) with roster count {RosterCountBefore}", agentName, pickInRound, _draftState.Round, _draftState.Pick, rosterCountBefore);
                if (rosterCountBefore >= maxRosterSize)
                {
                    _logger.LogWarning("Agent {AgentName} already has a full roster with {RosterCountBefore} players. Skipping pick.", agentName, rosterCountBefore);
                    _draftState.Pick++;
                    await SaveDraftStateAsync();
                    continue;
                }

                AgentResponse? response = null;
                // Step 1: Let the agent try. This includes retries in case of transient errors up to the maximum number of draft pick attempts.
                response = await DraftPlayerAsync(agent, _draftState.Round, _draftState.Pick, maxDraftPickAttempts);

                // Step 2: Verify roster actually grew.
                var rosterCountAfter = await GetAgentRosterCountAsync(agentName);
                if (rosterCountAfter <= rosterCountBefore)
                {
                    // Looks like it did not increase, so we will retry once more according to our retry logic.
                    _logger.LogWarning("Agent {AgentName} did not add a player on first attempt. Retrying...", agentName);
                    response = await DraftPlayerAsync(agent, _draftState.Round, _draftState.Pick, maxDraftPickAttempts, response?.Text);
                    rosterCountAfter = await GetAgentRosterCountAsync(agentName);
                }

                // Step 3: If still no player, auto-draft
                if (rosterCountAfter <= rosterCountBefore)
                {
                    _logger.LogWarning("Agent {AgentName} failed after retry. Auto-drafting best available.", agentName);
                    await MakeAutoDraftPickAsync(agentName);
                    // Now lets add an entry to the decision log so we can track that this pick was auto-drafted due to agent failure.
                    var decisionText = "Auto-drafted best available player due to agent failure.";
                    await DecisionLogger.LogDecisionAsync(agentName, 0, "Add Player", decisionText, "Draft", _logger);
                    rosterCountAfter = await GetAgentRosterCountAsync(agentName);
                }

                var pickSucceeded = rosterCountAfter > rosterCountBefore;
                _logger.LogInformation("Pick complete — Round={Round} Pick={Pick} PickInRound={PickInRound} Agent={Agent} Success={Success}", _draftState.Round, _draftState.Pick, pickInRound, agentName, pickSucceeded);
                if(pickSucceeded)
                {
                    await DecisionLogger.LogDecisionAsync(agentName, 0, "Draft Pick", response, $"Round { _draftState.Round} Pick {pickInRound}", _logger);
                }
                _draftState.Pick++;
                await SaveDraftStateAsync();
            }
        }

        // Draft is complete! Update state and save.
        _draftState.IsDraftComplete = true;
        await SaveDraftStateAsync(writeIndented: true);
        _logger.LogInformation("Draft is complete!");
        _logger.LogInformation("Prompting Agents to review their rosters and update their strategy based on the players they drafted...");
        await RunPostDraftAsync(_agents);
        _logger.LogInformation("Post-draft review complete!");

        return _draftState;
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

        var agentsByName = _agents.Where(agent => !string.IsNullOrWhiteSpace(agent.GetAgentName())).ToDictionary(agent => agent.GetAgentName(), StringComparer.OrdinalIgnoreCase);

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
    async Task<AgentResponse> DraftPlayerAsync(FantasyAgent agent, int round, int pick, int maxAttempts, string? previousAttemptText = null)
    {
        var previousAttemptPrompt = string.Empty;
        if (!string.IsNullOrWhiteSpace(previousAttemptText))
        {
            previousAttemptPrompt = $"""
                Your previous attempt did not result in a player being added to your roster.
                Here is what you said last time: "{previousAttemptText}"
                Try again. Call `GetAvailablePlayers` and pick a currently available player.
            """;
        }
        // The main prompt being passed to the agent for drafting.
        var draftPrompt = $"""
            The leauge is drafting, and you're up to select a player! You are allowed to add exactly one player to your roster.
            This is currently round {round} of {totalRounds} total rounds, and this is pick {pick} of {totalRounds * _agents.Count} total picks.
            {previousAttemptPrompt}
            Look at your roster using the `GetMyRoster` tool and identify what player you need to draft next.
            If you haven't already use the `ReadAgentBootstrap` tool to read your bootstrap file and see your strategy and roster needs.
            Call `GetAvailablePlayers` filtered by the needed position to see players who are still available.
            Use the `SearchWeb` tool to research the available players.
            Call `MakeRosterMove` at most one time with agentId {agent.GetAgentName()} and the selected player's Sleeper ID as addSleeperPlayerId.
            Once `MakeRosterMove` succeeds with result status `completed`, stop calling tools and respond.
            Do not add a backup/second player in this turn.
            If `MakeRosterMove` fails, stop calling tools and report the exact failure.
            Update your Bootstrap file using the `WriteAgentBootstrap` tool to update your roster, strategy, or insights on your next pick based on the player you drafted.
            When you're done, respond with the name of the player you added and why. This is your team so the reasoning for your pick should come from your perspective as the agent making the pick, based on your strategy and team needs.
        """;

        // Sometimes things go slow, the agent might not respond in time, or there might be transient errors.
        var draftPickRetryBackoffs = new[] { TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(90) };
        var agentName = agent.GetAgentName() ?? throw new InvalidOperationException("Draft agent name is required before making picks.");

        AgentResponse response = new();
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                // Provided there are no transient errors, this is the main call to the agent to make a draft pick.
                response = (await agent.RunAsync(draftPrompt)).Response;
                return response;
            }
            catch (Exception ex) when (IsDraftPickFailure(ex) && attempt < maxAttempts)
            {
                // Okay so there was an error, lets just wait a bit and try again according to our backoff schedule.
                var retryBackoff = draftPickRetryBackoffs[Math.Min(attempt - 1, draftPickRetryBackoffs.Length - 1)];
                _logger.LogWarning(ex, "Draft pick {Pick} in round {Round} for agent {AgentName} failed on attempt {Attempt}/{MaxAttempts}; retrying in {RetryDelaySeconds} seconds.", pick, round, agentName, attempt, maxAttempts, retryBackoff.TotalSeconds);
                await Task.Delay(retryBackoff);
            }
            catch (Exception ex) when (IsDraftPickFailure(ex))
            {
                // We tried too many times and exhausted all of our retry attempts, so we are giving up on this draft pick for this agent.
                _logger.LogWarning(ex, "Draft pick {Pick} in round {Round} for agent {AgentName} failed after {MaxAttempts} attempts.", pick, round, agentName, maxAttempts);
            }
        }
        return response;
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

    // Once the draft is done, the agents need to look though their rosters, assign them to starting positions, and update their strategy.
    public async Task RunPostDraftAsync(List<FantasyAgent> fantasyAgents)
    {
        var prompt = await _blobStorageTools.GetPromptFromBlobStorageAsync("FantasyAgent.post-draft.md");
        foreach(var agent in fantasyAgents)
        {
            var response = (await agent.RunAsync(prompt)).Response;
            _logger.LogInformation($"Post-draft response from {agent.GetAgentName()}: {response}");
            await DecisionLogger.LogDecisionAsync(agent.GetAgentName()!, 0, "Post-Draft Review", response, "Post-Draft", _logger);
        }
    }
    
    // This is another helper method to get the current number of players on an agent's roster.
    // We use this to check how many players an agent currently has on their roster. Then, compare it to after the draft.
    async Task<int> GetAgentRosterCountAsync(string agentId)
    {
        var roster = await _http.GetFromJsonAsync<List<object>>($"/api/rosters/{agentId}");
        return roster?.Count ?? 0;
    }

    // When the agent fails at making a draft pick, we're going to auto pick one for them by selecting the best available player.
    async Task<string?> GetBestAvailablePlayerAsync()
    {
        var bestAvailablePlayers = await _http.GetFromJsonAsync<List<System.Text.Json.JsonElement>>("api/players/available?limit=1");
        if (bestAvailablePlayers?.Count > 0)
        {
            var playerId = bestAvailablePlayers[0].GetProperty("player").GetProperty("sleeperPlayerId").GetString();
            return playerId;
        }
        return null;
    }

    // This is taking the best available player from the pool of undrafted players and drafting it for the player.
    // They fail to make a draft pick themselves, so this method steps in and automatically drafts the best available player for them.
    async Task<bool> MakeAutoDraftPickAsync(string agentId)
    {
        var bestAvailablePlayerId = await GetBestAvailablePlayerAsync();
        if (bestAvailablePlayerId != null)
        {
            try
            {
                var result = await _http.PostAsJsonAsync("/api/league/roster-moves", new
                {
                    agentId,
                    addSleeperPlayerId = bestAvailablePlayerId,
                    acquisitionSource = "auto-draft"
                });
                if (!result.IsSuccessStatusCode)
                {
                    var body = await result.Content.ReadAsStringAsync();
                    _logger.LogWarning("Auto draft failed for agent {AgentId} player {PlayerId}: {StatusCode} — {Body}", agentId, bestAvailablePlayerId, (int)result.StatusCode, body);
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to auto draft player {PlayerId} for agent {AgentId}", bestAvailablePlayerId, agentId);
            }
        }
        return false;
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

}
