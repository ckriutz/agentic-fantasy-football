using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

var host = builder.Build();
var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Program");

HttpClient _http = new() { BaseAddress = new Uri("http://localhost:5000/") };

logger.LogInformation("Booting up Agentic Fantasy Football League...");
// I want to print out a few things before we start the league, just to make sure everything is working correctly.

// First, the location for where the agents will store their bootstrapping information.
logger.LogInformation("Agents will store their bootstrapping information in the following directory: " + EnvironmentVariableHelper.GetRequired("AZURE_STORAGE_CONTAINER_NAME"));

// Second, I want to make sure the API is up and running, and that we can connect to it successfully.
try
{
    var apiResponse = await _http.GetAsync("/");
    if (apiResponse.IsSuccessStatusCode)
    {
        logger.LogInformation("Successfully connected to the API. API is healthy.");
    }
    else
    {
        logger.LogError("Failed to connect to the API. Status code: " + apiResponse.StatusCode);
        return;
    }
}
catch (Exception ex)
{
    logger.LogError("An error occurred while trying to connect to the API: " + ex.Message);
    return;
}

// Now the current leauge state.
var leagueStateResponse = await _http.GetAsync("api/league/state");
if (leagueStateResponse.IsSuccessStatusCode)
    {
        // Deserialize the league state and print out the current phase of the league.
        var leagueStateJson = await leagueStateResponse.Content.ReadAsStringAsync();
        var leagueState = System.Text.Json.JsonSerializer.Deserialize<dynamic>(leagueStateJson, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        logger.LogInformation($"Current League State - Season: {leagueState.GetProperty("season")}, Week: {leagueState.GetProperty("week")}, Phase: {leagueState.GetProperty("phase")}");
    }

logger.LogInformation("Starting Agentic Fantasy Football League...");

var response = await _http.GetAsync("api/agent-profiles?enabledOnly=false");
response.EnsureSuccessStatusCode();
var agentProfilesJson = await response.Content.ReadAsStringAsync();
var agentProfiles = System.Text.Json.JsonSerializer.Deserialize<List<AgenticLeague.Models.AgentProfile>>(agentProfilesJson, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
List<FantasyAgent> agents = new List<FantasyAgent>();


//Console.WriteLine("Testing one agent to make sure the bootstrapping process works...");
//FantasyAgent testAgent = new FantasyAgent(new AgenticLeague.Models.AgentConfig
//{
//    AgentName = "test-player-01",
//    Model = "google/gemini-3.5-flash",
//    Connection = "OpenRouter"
//});
//await testAgent.InitializeAsync();
//await testAgent.EnsureBootstrappedAsync();

logger.LogInformation("Initializing agents...");
foreach (var agentConfig in agentProfiles.Where(p => p.IsEnabled))
{
    // In this loop, we're connecting to all the agents, and making sure they're initialized and bootstrapped before we start the league.
    // This is important because we want to make sure all agents are ready to go before we start the draft, and it also allows us to catch any issues with initialization or bootstrapping early on.
    var fantasyAgent = new FantasyAgent(agentConfig);
    await fantasyAgent.InitializeAsync();
    await fantasyAgent.EnsureBootstrappedAsync();
    agents.Add(fantasyAgent);
}

logger.LogInformation("Number of agents initialized: " + agents.Count);


//logger.LogInformation("All agents are bootstrapped! Starting the draft...");

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

// Here, lets test the waver wire again.
var prompt = LoadPrompt("Prompts/FantasyAgent.waiver-claim.md");
var waverResponse = await agents.First(agent => agent.GetAgentName() == "player-05").RunAsync(prompt);
Console.WriteLine($"Waiver claim response from Player 5: {waverResponse}");

//var prompt = $"Use the `ReadAgentBootstrap` tool to read your bootstrap file, and then respond with a general summary of your current bootstrap status and team information based on the contents of the bootstrap file. If you don't have a bootstrap file, respond with 'No bootstrap file found.'.";
//var postResponse = await testAgent.RunAsync(prompt);
//Console.WriteLine($"Post-draft response from Test Agent 01: {postResponse}");


//var response4 = await agents.First(agent => agent.GetAgentName() == "player-04").RunAsync(prompt);
//Console.WriteLine($"Post-draft response from Player 4: {response4}");

//var response10 = await agents.First(agent => agent.GetAgentName() == "player-05").RunAsync(waverPrompt);
//Console.WriteLine($"Post-draft response from Player 5: {response10}");

 static string LoadPrompt(string relativePath)
 {
     var fullPath = Path.Combine(AppContext.BaseDirectory, relativePath);

     if (!File.Exists(fullPath))
     {
        throw new FileNotFoundException($"Prompt file not found at '{fullPath}'.",fullPath);
     }

     return File.ReadAllText(fullPath);
 }