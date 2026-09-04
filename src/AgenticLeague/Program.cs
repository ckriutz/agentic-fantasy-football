using AgenticLeague.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text.Json;

// First, determine the mode in which the application should run (e.g., test mode).
// If we mess this up we might as well abort immediately.
string? mode;
try
{
    mode = GetMode(args);
}
catch (ArgumentException ex)
{
    Console.Error.WriteLine(ex.Message);
    return;
}

// Lets set up the application.
var builder = Host.CreateApplicationBuilder(args);
var host = builder.Build();
var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Program");

HttpClient _http = new() { BaseAddress = new Uri(EnvironmentVariableHelper.GetRequired("API_BASE_URL")) };

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

if(await RunCreditCheckAsync(logger) < 2)
{
    logger.LogError("Not enough OpenRouter credits remaining to run the league. Please add more credits and try again.");
    return;
}

logger.LogInformation("✅ All checks passed. API is healthy.");
logger.LogInformation("🏈 Starting Agentic Fantasy Football League!");



// Now we go though each option for the mode, and run those methods.
if (string.Equals(mode, "bootstrap", StringComparison.OrdinalIgnoreCase))
{
    AgentProfile tProfile = new AgentProfile();
    tProfile.IsEnabled = true;
    tProfile.AgentId = "inception";
    tProfile.Connection = "OpenRouter";
    tProfile.ModelName = "inception/mercury-2.5-preview";
    
    FantasyAgent testAgent = new FantasyAgent(tProfile, host.Services.GetRequiredService<ILogger<FantasyAgent>>(), _http);

    await testAgent.InitializeAsync();
    await testAgent.EnsureBootstrappedAsync();

    return;
}

if (string.Equals(mode, "test", StringComparison.OrdinalIgnoreCase))
{
    logger.LogInformation("Running a test.");
    List<FantasyAgent> agents = await LoadAgentsAsync(_http, host, logger);
    var testAgent = agents.FirstOrDefault(a => a.GetAgentName() == "inception");
    //var result = await testAgent.RunAsync($"You are {testAgent.GetAgentName()}. Use that exact value for `ReadAgentBootstrap`, `WriteAgentBootstrap`, `SetMyTeamName`, and `SetMyBootstrapStatus`. Review your bootstrap and read through the core philosophy and the draft strategy. Then use the `searchWeb` tool and the `GetAvailablePlayers` tool in order to research and prepare for the draft. This is an opportunity to build out a more concrete plan for the upcoming draft. Once your research is complete, update your bootstrap file using the `WriteAgentBootstrap` tool. You're not finished unless you've written the research to the bootstrap file by using the `WriteAgentBootstrap` tool.");
    var result = await testAgent.RunAsync($"You are {testAgent.GetAgentName()}. Use that exact value for the `GenerateImage` tool. Your team name is Starlight Diffusion by the Inception company running the Mercury 2.5 llm. You need a logo for your team. Call `GenerateImage` with a concise logo description based on the team name. The logo must be simple and suitable for a fantasy-football website. This logo must work well for a blog. Be creative. Use the team name when generating the image.");
    logger.LogInformation("Agent {AgentId} produced response: {Response}", testAgent.GetAgentName(), result);
    return;
}

if(string.Equals(mode, "draft", StringComparison.OrdinalIgnoreCase))
{
    var leagueState = await LeagueStateHelper.GetLeagueStateAsync(_http, logger);
    HttpClient _scoresHttp = new() { BaseAddress = new Uri(EnvironmentVariableHelper.GetRequired("FANTASYPROS_SYNC_BASE_URL")) };
    logger.LogInformation("League state is valid. Current league phase: {Phase}", leagueState.Phase);
    List<FantasyAgent> agents = await LoadAgentsAsync(_http, host, logger);
    await RunDraftAsync(agents, leagueState.Phase, _http, host);
}

if(string.Equals(mode, "season", StringComparison.OrdinalIgnoreCase))
{
    var leagueState = await LeagueStateHelper.GetLeagueStateAsync(_http, logger);
    HttpClient _scoresHttp = new() { BaseAddress = new Uri(EnvironmentVariableHelper.GetRequired("FANTASYPROS_SYNC_BASE_URL")) };
    logger.LogInformation("League state is valid. Current league phase: {Phase}", leagueState.Phase);
    List<FantasyAgent> agents = await LoadAgentsAsync(_http, host, logger);
    await RunSeasonAsync(agents, leagueState.Phase, host, _http, _scoresHttp, leagueState);
}




static string? GetMode(string[] arguments)
{
    string? mode = null;

    for (var index = 0; index < arguments.Length; index++)
    {
        var argument = arguments[index];
        string? value = null;

        if (string.Equals(argument, "--mode", StringComparison.OrdinalIgnoreCase))
        {
            if (index + 1 >= arguments.Length || arguments[index + 1].StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException("The --mode option requires a value. Supported mode: test.");

            value = arguments[++index];
        }
        else if (argument.StartsWith("--mode=", StringComparison.OrdinalIgnoreCase))
        {
            value = argument["--mode=".Length..];
        }

        if (value is null)
            continue;
        if (mode is not null)
            throw new ArgumentException("The --mode option can only be supplied once.");

        mode = value.Trim().ToLowerInvariant();
    }

    // Here are the supported modes for the application. Currently, "test" and "echo" are supported.

    if (mode is not null && mode != "test" && mode != "echo" && mode != "season" && mode != "draft" && mode != "bootstrap")
        throw new ArgumentException($"Unsupported mode '{mode}'. Supported modes: test, echo, season, draft, bootstrap");

    return mode;
}

static async Task<List<FantasyAgent>> LoadAgentsAsync(HttpClient _http, IHost host, ILogger logger)
{
    // Load all the agents, and initialze them.
    var response = await _http.GetAsync("api/agent-profiles?enabledOnly=false");
    response.EnsureSuccessStatusCode();
    var agentProfilesJson = await response.Content.ReadAsStringAsync();
    var agentProfiles = JsonSerializer.Deserialize<List<AgenticLeague.Models.AgentProfile>>(agentProfilesJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
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
    return agents;
}

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

static async Task<double> RunCreditCheckAsync(ILogger logger)
{
    var creditCheckTool = new CreditCheckTool();
    var creditsResponse = await creditCheckTool.GetRemainingCreditsAsync(EnvironmentVariableHelper.GetRequired("OPENROUTER_API_KEY"));
    var remainingCredits = creditsResponse.TotalCredits - creditsResponse.TotalUsage;
    logger.LogInformation("Remaining OpenRouter Credits: " + remainingCredits);
    return remainingCredits;
}