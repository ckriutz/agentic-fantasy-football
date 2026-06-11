using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;

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
if (!leagueStateResponse.IsSuccessStatusCode)
{
    logger.LogError("Failed to load league state. Status code: " + leagueStateResponse.StatusCode);
    return;
}

var leagueStateJson = await leagueStateResponse.Content.ReadAsStringAsync();
var leagueState = JsonSerializer.Deserialize<JsonElement>(leagueStateJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
if (leagueState.ValueKind != JsonValueKind.Object)
{
    logger.LogError("League state response was not a JSON object.");
    return;
}

var phase = leagueState.GetProperty("phase").GetString();
if (string.IsNullOrWhiteSpace(phase))
{
    logger.LogError("League state phase was missing from the API response.");
    return;
}

logger.LogInformation("Starting Agentic Fantasy Football League...");

// Load all the agents.
var response = await _http.GetAsync("api/agent-profiles?enabledOnly=false");
response.EnsureSuccessStatusCode();
var agentProfilesJson = await response.Content.ReadAsStringAsync();
var agentProfiles = System.Text.Json.JsonSerializer.Deserialize<List<AgenticLeague.Models.AgentProfile>>(agentProfilesJson, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
List<FantasyAgent> agents = new List<FantasyAgent>();
var fantasyAgentLogger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger<FantasyAgent>();

logger.LogInformation("Initializing agents...");
foreach (var agentConfig in agentProfiles.Where(p => p.IsEnabled))
{
    // In this loop, we're connecting to all the agents, and making sure they're initialized and bootstrapped before we start the league.
    // This is important because we want to make sure all agents are ready to go before we start the draft, and it also allows us to catch any issues with initialization or bootstrapping early on.
    var fantasyAgent = new FantasyAgent(agentConfig, fantasyAgentLogger);
    await fantasyAgent.InitializeAsync();
    await fantasyAgent.EnsureBootstrappedAsync();
    agents.Add(fantasyAgent);
}

logger.LogInformation("Number of agents initialized: " + agents.Count);

// Lets look at the Yahoo Status
//var yahooLogger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger<YahooRunner>();
//YahooRunner yahooRunner = new YahooRunner(yahooLogger);
//await yahooRunner.RunAsync();


if (string.Equals(phase, "drafting", StringComparison.OrdinalIgnoreCase))
{
    logger.LogInformation("League state is drafting. Starting the draft runner...");
    DraftRunner draftRunner = new DraftRunner(agents, logger);
    await draftRunner.RunDraftAsync();
    logger.LogInformation("Draft runner completed.");
}
else
{
    logger.LogInformation("Skipping draft runner because league phase is {Phase}.", phase);
}

var bst = new BlobStorageTools();
var promptFile = await bst.GetPromptFromBlobStorageAsync("FantasyAgent.how-to-play.md");
logger.LogInformation("Prompt content loaded from Blob Storage: " + promptFile.Substring(0, Math.Min(200, promptFile.Length)) + "...");

// Here, lets test the waver wire again.
//var prompt = LoadPrompt("Prompts/FantasyAgent.waiver-claim.md");
//var waverResponse = await agents.First(agent => agent.GetAgentName() == "player-04").RunAsync(prompt);
//Console.WriteLine($"Waiver claim response from Player 4: {waverResponse}");
//logger.LogInformation("Input tokens used: " + waverResponse.Usage.InputTokenCount);
//logger.LogInformation("Output tokens used: " + waverResponse.Usage.OutputTokenCount);
//logger.LogInformation("Total tokens used: " + waverResponse.Usage.TotalTokenCount);

//var prompt = $"Use the `ReadAgentBootstrap` tool to read your bootstrap file, and then respond with a general summary of your current bootstrap status and team information based on the contents of the bootstrap file. If you don't have a bootstrap file, respond with 'No bootstrap file found.'.";
//var postResponse = await testAgent.RunAsync(prompt);
//Console.WriteLine($"Post-draft response from Test Agent 01: {postResponse}");


//var response4 = await agents.First(agent => agent.GetAgentName() == "player-04").RunAsync(prompt);
//Console.WriteLine($"Post-draft response from Player 4: {response4}");

//var response10 = await agents.First(agent => agent.GetAgentName() == "player-05").RunAsync(waverPrompt);
//Console.WriteLine($"Post-draft response from Player 5: {response10}");
