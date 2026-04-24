using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.Agents.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

var host = builder.Build();
var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Program");

List<AIAgent> agents = new List<AIAgent>();

//var dsa = await new DataStatusAgent().CreateDataStatusAgentAsync();
var player1 = await new FantasyAgent().CreateFantasyAgentAsync("player-01", "x-ai/grok-4.20");
var player2 = await new FantasyAgent().CreateFantasyAgentAsync("player-02", "google/gemini-3-flash-preview");
var player3 = await new FantasyAgent().CreateFantasyAgentAsync("player-03", "anthropic/claude-sonnet-4.6");
var player4 = await new FantasyAgent().CreateFantasyAgentAsync("player-04", "nvidia/nemotron-3-super-120b-a12b");
var player5 = await new FantasyAgent().CreateFantasyAgentAsync("player-05", "openai/gpt-5.4");
var player6 = await new FantasyAgent().CreateFantasyAgentAsync("player-06", "moonshotai/kimi-k2.6");
var player7 = await new FantasyAgent().CreateFantasyAgentAsync("player-07", "z-ai/glm-5.1");
var player8 = await new FantasyAgent().CreateFantasyAgentAsync("player-08", "deepseek/deepseek-v3.2");
var player9 = await new FantasyAgent().CreateFantasyAgentAsync("player-09", "minimax/minimax-m2.7");
var player10 = await new FantasyAgent().CreateFantasyAgentAsync("player-10", "mistralai/mistral-small-2603");
agents.Add(player1);
agents.Add(player2);
agents.Add(player3);
agents.Add(player4);
agents.Add(player5);
agents.Add(player6);
agents.Add(player7);
agents.Add(player8);
agents.Add(player9);
agents.Add(player10);

// First step is to have each agent check if they're bootstrapped, and if not, begin that process.
//This will involve them creating a bootstrap file with their team name, strategy, and logo, and initializing their profile with this information.
//They should respond back with their team name and a quick summary of their strategy.
/*
foreach(var agent in agents)
{
    var bootstrapPrompt = """
    You're being initilized. Check to see if you're bootstrapped, and if not, begin that process. Once bootstrapped, respond back with your team name and quick summary of your strategy. 
    If you are bootstrapped already, just check to make sure the bootstrap.md file is complete and the profile.json file is correct and then respond with: ✅ (your team name) is bootstrapped and ready to go!"
    There is no need, if you're bootstrapped, to respond with your strategy again. Just confirm that you're bootstrapped and ready to go.
    """;
        """;
    try
    {
        var response = await agent.RunAsync(bootstrapPrompt);
        logger.LogInformation("Agent {agentName} response: {Response}", agent.Name, response.Text);
    }
    catch (ArgumentOutOfRangeException ex) when (ex.Message.Contains("ChatFinishReason"))
    {
        logger.LogWarning("Agent {agentName} returned unknown finish reason — skipping", agent.Name);
    }
}
*/



// Going to simulate the draft. Since this is a snake draft, the order will reverse every other round.
// So in round 1, the order will be 1-10, in round 2, the order will be 10-1, and so on.
// Each agent will look at their roster and identify if they have room for additional players.
// If so, they'll use the tools available to them to do research and find a player to add to their roster.
// Then they'll use the tools to add that player to their roster. They'll select ONE player only.
// They'll respond with the name of the player they added and why they chose that player based on their strategy and team needs.
logger.LogInformation("Starting the draft!");

// To make things fair, lets randomize the order of the agents before starting the draft.
// This will ensure that no agent has an inherent advantage based on their position in the draft order.

agents = agents.OrderBy(a => Guid.NewGuid()).ToList();
int pick = 1;
for(int round = 1; round <= 15; round++)
{
    logger.LogInformation("Starting round {Round}", round);
    var currentAgents = round % 2 == 1 ? agents : agents.AsEnumerable().Reverse();
    foreach(var agent in currentAgents)
    {
        var response = await agent.RunAsync($"The leauge is drafting, and you're up to select a player! This is currently round {round} of 15 total rounds, and this is pick {pick} of 150 total picks. Look at your roster and identify what player you need to draft next. Use the tools available to you to do research and find a player to add to your roster. Then use the tools to add that player to your roster. Select ONE player only. Respond with the name of the player you added and why you chose that player based on your strategy and team needs.");
        logger.LogInformation("Agent {agentName} response: {Response}", agent.Name, response.Text);
        await LogDecisionAsync(agent.Name, 0, "Add Player", response, "Draft", logger);
        pick++;
    }
}

logger.LogInformation("Draft is complete!");


//foreach(var agent in agents)
//{
//    var response = await agent.RunAsync("Look at your roster and identify if you have room for additional players. If so, use the tools available to you to do research and find a player to add to your roster. Then use the tools to add that player to your roster. Select ONE player only. Respond with the name of the player you added and why you chose that player based on your strategy and team needs.");
//    logger.LogInformation("Agent {agentName} response: {Response}", agent.Name, response);
//    await LogDecisionAsync(agent.Name, 0, "AddPlayer", response.Text, "Draft", logger);
//}


static async Task LogDecisionAsync(string agentId, int week, string type, AgentResponse response, string action, ILogger logger)
{
    using var http = new HttpClient { BaseAddress = new Uri("http://localhost:5000/") };
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
        var decisioonResponse = await http.PostAsJsonAsync("/api/decisions", payload);
        decisioonResponse.EnsureSuccessStatusCode();
        logger.LogInformation("Logged decision for {AgentId}: {Type} - {Action}", agentId, type, action);
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Failed to log decision for {AgentId}", agentId);
    }
}