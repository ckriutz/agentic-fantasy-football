using System.Reflection.Metadata.Ecma335;
using System.Xml.Schema;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

var host = builder.Build();
var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Program");

logger.LogInformation("Starting Agentic Fantasy Football League...");

List<FantasyAgent> agents = new List<FantasyAgent>();

// Load the agents.config.json file to get the list of agents and their corresponding models.
var agentsConfigPath = Path.Combine(AppContext.BaseDirectory, "agents.config.json");
if (!File.Exists(agentsConfigPath))
{
    logger.LogError("Agents configuration file not found at '{path}'. Please create an agents.config.json file with the list of agents and their models.", agentsConfigPath);
    return;
}

// Read in the agents configuration and initialize each agent accordingly.
var agentsConfigJson = await File.ReadAllTextAsync(agentsConfigPath);
var agentsConfig = System.Text.Json.JsonSerializer.Deserialize<List<Dictionary<string, string>>>(agentsConfigJson);
foreach(var agentConfig in agentsConfig)
{
    // In this loop, we're connecting to all the agents, and making sure they're initialized and bootstrapped before we start the league.
    // This is important because we want to make sure all agents are ready to go before we start the draft, and it also allows us to catch any issues with initialization or bootstrapping early on.
    var agentName = agentConfig["agentName"].ToString()!;
    var model = agentConfig["model"].ToString()!;
    var fantasyAgent = new FantasyAgent(agentName, model);
    await fantasyAgent.InitializeAsync();
    await fantasyAgent.EnsureBootstrappedAsync();
    agents.Add(fantasyAgent);
}

logger.LogInformation("All agents are bootstrapped! Starting the draft...");

// Going to simulate the draft. Since this is a snake draft, the order will reverse every other round.
// So in round 1, the order will be 1-10, in round 2, the order will be 10-1, and so on.
// Each agent will look at their roster and identify if they have room for additional players.
// If so, they'll use the tools available to them to do research and find a player to add to their roster.
// Then they'll use the tools to add that player to their roster. They'll select ONE player only.
// They'll respond with the name of the player they added and why they chose that player based on their strategy and team needs.
// To make things fair, lets randomize the order of the agents before starting the draft.
// This will ensure that no agent has an inherent advantage based on their position in the draft order.
//DraftRunner draftRunner = new DraftRunner(agents, logger);
//await draftRunner.RunDraftAsync();
//logger.LogInformation("Draft is complete!");

// Now that the draft is complete, the agentes need to review theitr rosters and place their players on the appropriate slots on their roster (e.g. starting lineup, bench, injured reserve, etc.) based on their strategy and the players they drafted.
//var postDraftPromptPath = Path.Combine(AppContext.BaseDirectory, "Prompts/FantasyAgent.post-draft.md");
//var prompt = await File.ReadAllTextAsync(postDraftPromptPath);
//var postResponse = await agents.First(agent => agent.GetAgentName() == "player-04").RunAsync(prompt);
//Console.WriteLine($"Post-draft response from Player 4: {postResponse}");

var prompt = $"Your roster should be full, but use the `AddPlayerToRoster` tool to add a player to your roster that you think would be a good fit for your team based on your strategy and team needs. Since your roster is full, this tool should fail, If it fails, report the failure. If it does succeed, double check that the playe was added by using the `GetMyRoster` tool and reporting back the player you added.";
var postResponse = await agents.First(agent => agent.GetAgentName() == "player-05").RunAsync(prompt);
Console.WriteLine($"Post-draft response from Player 5: {postResponse}");