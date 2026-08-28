using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text.Json;

var builder = Host.CreateApplicationBuilder(args);

var host = builder.Build();
var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Program");

HttpClient _http = new() { BaseAddress = new Uri(EnvironmentVariableHelper.GetRequired("API_BASE_URL")) };
HttpClient _scoresHttp = new() { BaseAddress = new Uri(EnvironmentVariableHelper.GetRequired("FANTASYPROS_SYNC_BASE_URL")) };

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

var creditCheckTool = new CreditCheckTool();
var creditsResponse = await creditCheckTool.GetRemainingCreditsAsync(EnvironmentVariableHelper.GetRequired("OPENROUTER_API_KEY"));
var remainingCredits = creditsResponse.TotalCredits - creditsResponse.TotalUsage;
logger.LogInformation("Remaining OpenRouter Credits: " + remainingCredits);

if(remainingCredits < 2)
{
    logger.LogError("Not enough OpenRouter credits remaining to run the league. Please add more credits and try again.");
    return;
}

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
logger.LogInformation("testing.");


await RunSeasonAsync(agents, leagueState.Phase, host, _http, _scoresHttp, leagueState);


static async Task RunDraftAsync(List<FantasyAgent> agents, string phase, HttpClient _http, IHost host)
{
    ILogger draftLogger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("DraftRunner");
    if (string.Equals(phase, "drafting", StringComparison.OrdinalIgnoreCase))
    {
        draftLogger.LogInformation("League state is drafting. Starting the draft runner...");
        DraftRunner draftRunner = new DraftRunner(agents, draftLogger, _http);
        var draftState = await draftRunner.RunDraftAsync();
        draftLogger.LogInformation("🎉 Draft runner completed.");

        var seedWaiverPriorityResponse = await _http.PostAsJsonAsync("api/league/waivers/priority/seed", new
        {
            draftOrder = draftState.DraftOrder
        });
        seedWaiverPriorityResponse.EnsureSuccessStatusCode();

        var leagueState = await LeagueStateHelper.GetLeagueStateAsync(_http, draftLogger);
        var generateScheduleResponse = await _http.PostAsync($"api/league/seasons/{leagueState.Season}/schedule", null);
        generateScheduleResponse.EnsureSuccessStatusCode();

        var advanceResponse = await _http.PutAsJsonAsync("api/league/state", new
        {
            season = leagueState.Season,
            week = 1,
            phase = "free_agency",
            seasonStage = "regular_season",
            updatedBy = "season-runner"
        });
        advanceResponse.EnsureSuccessStatusCode();
        draftLogger.LogInformation("Post-draft setup completed for season {Season}.", leagueState.Season);
    }
    else
    {
        draftLogger.LogInformation("Skipping draft runner because league phase is {Phase}.", phase);
    }
}

static async Task RunSeasonAsync(List<FantasyAgent> agents, string phase, IHost host, HttpClient http, HttpClient scoresHttp, LeagueState leagueState)
{
    ILogger seasonLogger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("SeasonRunner");
    // We don't want to run the season runner if we are in the drafting phase, because the draft runner will handle moving the league into the free-agency phase once the draft is complete.
    if (string.Equals(phase, "drafting", StringComparison.OrdinalIgnoreCase))
    {
        seasonLogger.LogInformation("Skipping season runner because league is in drafting phase.");
        return;
    }
    seasonLogger.LogInformation("League state is {Phase}. Starting the season runner...", phase);

    SeasonRunner seasonRunner = new SeasonRunner(agents, seasonLogger, http, scoresHttp, leagueState);
    await seasonRunner.RunAsync();
    seasonLogger.LogInformation("🎉 Season runner completed.");
}
