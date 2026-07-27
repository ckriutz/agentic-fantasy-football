using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text.Json;

var builder = Host.CreateApplicationBuilder(args);

var host = builder.Build();
var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Program");

HttpClient _http = new() { BaseAddress = new Uri(EnvironmentVariableHelper.GetRequired("API_BASE_URL")) };
HttpClient _yahooHttp = new() { BaseAddress = new Uri(EnvironmentVariableHelper.GetRequired("YAHOO_API_BASE_URL")) };

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

// Now the current leauge state. This is important because we want to make sure the league is in the correct state before we start running the agents.
var leagueState = await LeagueStateHelper.GetLeagueStateAsync(_http, logger);


logger.LogInformation("✅ All checks passed. API is healthy, and league state is valid. Current league phase: " + leagueState.Phase);
logger.LogInformation("🏈 Starting Agentic Fantasy Football League!");

// Load all the agents, and initialze them.
var response = await _http.GetAsync("api/agent-profiles?enabledOnly=false");
response.EnsureSuccessStatusCode();
var agentProfilesJson = await response.Content.ReadAsStringAsync();
var agentProfiles = System.Text.Json.JsonSerializer.Deserialize<List<AgenticLeague.Models.AgentProfile>>(agentProfilesJson, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
List<FantasyAgent> agents = new List<FantasyAgent>();
var fantasyAgentLogger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger<FantasyAgent>();

logger.LogInformation("🤖 Initializing agents...");
foreach (var agentConfig in agentProfiles.Where(p => p.IsEnabled))
{
    // In this loop, we're connecting to all the agents, and making sure they're initialized and bootstrapped before we start the league.
    // This is important because we want to make sure all agents are ready to go before we start the draft, and it also allows us to catch any issues with initialization or bootstrapping early on.
    var fantasyAgent = new FantasyAgent(agentConfig, fantasyAgentLogger, _http);
    await fantasyAgent.InitializeAsync();
    await fantasyAgent.EnsureBootstrappedAsync();
    agents.Add(fantasyAgent);
}

logger.LogInformation("✅ Success! Number of agents initialized: " + agents.Count);

// Now to run the draft, if the league is in the drafting phase. If not, we can skip this step and move on to the season runner.
await RunDraftAsync(agents, leagueState.Phase, _http, host);

// Since I'm casaully testing, I don't want to pass ALL the agents in, just a few.
//logger.LogInformation("testing.");
//var testAgents = agents.Where(a => a.GetAgentName() == "player-08" || a.GetAgentName() == "player-09" || a.GetAgentName() == "player-10").ToList();
//var prompt = """test""";
//var testAgent = agents.First(agent => agent.GetAgentName() == "player-03");
//var testResponse = await testAgent.RunAsync("Look at your roster, and tell me if your starting roster is full or not. If it is, respond with 'Starting roster is full.'. If it is not, respond with what position is missing a starting player and which bench player you would fill it with. Don't make any moves, just evaluate. You MUST provide a response.");
//Console.WriteLine($"Response from Player 1: {testResponse.Response}");

await RunSeasonAsync(agents, leagueState.Phase, host, _http, _yahooHttp, leagueState);

static async Task RunDraftAsync(List<FantasyAgent> agents, string phase, HttpClient _http, IHost host)
{
    ILogger draftLogger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("DraftRunner");
    if (string.Equals(phase, "drafting", StringComparison.OrdinalIgnoreCase))
    {
        draftLogger.LogInformation("League state is drafting. Starting the draft runner...");
        DraftRunner draftRunner = new DraftRunner(agents, draftLogger, _http);
        await draftRunner.RunDraftAsync();
        draftLogger.LogInformation("🎉 Draft runner completed.");

        // Move the league from drafting into the free-agency phase so the agents can
        // start making roster moves once the draft is complete.
        var advanceResponse = await _http.PutAsJsonAsync("api/league/state", new
        {
            phase = "free_agency",
            updatedBy = "season-runner"
        });
        advanceResponse.EnsureSuccessStatusCode();
    }
    else
    {
        draftLogger.LogInformation("Skipping draft runner because league phase is {Phase}.", phase);
    }
}

static async Task RunSeasonAsync(List<FantasyAgent> agents, string phase, IHost host, HttpClient http, HttpClient yahooHttp, LeagueState leagueState)
{
    ILogger seasonLogger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("SeasonRunner");
    // We don't want to run the season runner if we are in the drafting phase, because the draft runner will handle moving the league into the free-agency phase once the draft is complete.
    if (string.Equals(phase, "drafting", StringComparison.OrdinalIgnoreCase))
    {
        seasonLogger.LogInformation("Skipping season runner because league is in drafting phase.");
        return;
    }
    seasonLogger.LogInformation("League state is {Phase}. Starting the season runner...", phase);
    SeasonRunner seasonRunner = new SeasonRunner(agents, seasonLogger, http, yahooHttp, leagueState);
    await seasonRunner.RunAsync();
    seasonLogger.LogInformation("🎉 Season runner completed.");

}


// Lets test the players ability to move a player onto the bench.
//var agent = agents.First(agent => agent.GetAgentName() == "player-01");
//var prompt = "Run the skill smoke test. Don't do anything else.";
//var testresponse = await agent.RunAsync(prompt);
//Console.WriteLine($"Response from Player 1: {testresponse}");

// Here, lets test free agency.
//var prompt = "Use the `weekly-player-management` skill to evaluate your roster and make any necessary moves.";
//var waverResponse = await agents.First(agent => agent.GetAgentName() == "player-05").RunAsync(prompt);
//Console.WriteLine($"Response from Player 5: {waverResponse}");
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
