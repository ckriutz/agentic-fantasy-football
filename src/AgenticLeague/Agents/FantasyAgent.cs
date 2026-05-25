using System.ClientModel;
using ModelContextProtocol.Client;
using AgenticLeague.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;

public class FantasyAgent
{
    private static readonly string endpoint = Environment.GetEnvironmentVariable("OPENROUTER_BASE_URL") ?? "https://openrouter.ai/api/v1";
    private static readonly string apiKey = GetRequiredEnvironmentVariable("OPENROUTER_API_KEY");
    private AIAgent? _agent;
    HttpClient httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:5000/") };
    private McpClient? _leagueApiMcpClient;
    private readonly string _modelName;
    private readonly string _agentId;
    private readonly string _agentConnection;

    public FantasyAgent(string agentId, string modelName)
    {
        _agentId = agentId;
        _modelName = modelName;
    }

    public FantasyAgent(AgentConfig config)
    {
        _agentId = config.AgentName;
        _modelName = config.Model;
        _agentConnection = config.Connection;
    }

    public string GetAgentName() => _agentId;

    public async Task InitializeAsync()
    {
        if (_agent != null) { return; } // Already initialized, no need to do it again.

        //var agentProfileTools = new AgentProfileTools();
        var blobStorageTools = new BlobStorageTools();
        var imageGenerationTool = new ImageGenerationTool(_agentId);
        var searchTool = new SearchTool();
        
        var leaguePrompt = LoadPrompt("Prompts/FantasyAgent.league.md");
        var howToPlayPrompt = LoadPrompt("Prompts/FantasyAgent.how-to-play.md");

        // The LeaugeAPI has a LOT of tools as well, and this is how we get to them.
        var mcpTransport = new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = new Uri("http://localhost:5000/mcp"),
            Name = "LeagueAPI"
        });

        // Now, connect to the LeagueAPI MCP endpoint to get the tools we can use to interact with the league, such as viewing the draft board, making trades, adding players to our roster, etc.
        _leagueApiMcpClient = await McpClient.CreateAsync(mcpTransport);
        IList<McpClientTool> mcpTools = await _leagueApiMcpClient.ListToolsAsync();

        var agentInstructions =
        $"""
        You are {_agentId}, a fantasy football manager, and your job is to manage your fantasy football team to victory.
        
        Your current team name, strategy, status, and memory can be found by using `ReadAgentBootstrap` tool to read your bootstrapping file. Always read this file before making any decisions.

        Here are the fantasy football league rules and settings that you should be aware of:
        {leaguePrompt}

        Here are instructions on how to play fantasy football and manage your team:
        {howToPlayPrompt}

        Use the `SearchWeb` tool whenever you need current external research about players, injuries, depth charts, rankings, or matchup context before making a move.
        Use the `ReadAgentBootstrap` and `WriteAgentBootstrap` tools to read and write your bootstrap file, which contains your strategy, team name, logo path, and bootstrap status.
        This is where you should keep any information about your team that you want to remember across interactions.
        """;

        ChatClient? chatClient = null;
        OpenAIClientOptions options = null;
        if(_agentConnection == "OpenRouter")
        {
            options = new OpenAIClientOptions
            {
                Endpoint = new Uri(endpoint),
                NetworkTimeout = TimeSpan.FromMinutes(5),
                ProjectId = "agentic-fantasy-football",
                UserAgentApplicationId = "AgenticFantasyFootball"
            };
            chatClient = new ChatClient(_modelName, new ApiKeyCredential(apiKey), options);
        }
        if(_agentConnection == "MSFoundry")
        {
            var key = Environment.GetEnvironmentVariable("FoundryKey");
            var endpoint = Environment.GetEnvironmentVariable("FoundryEndpoint");
            options = new OpenAIClientOptions
            {
                Endpoint = new Uri(endpoint),
                NetworkTimeout = TimeSpan.FromMinutes(5),
            };
            chatClient = new ChatClient(_modelName, new ApiKeyCredential(key), options);
        }

        _agent = chatClient
            .AsIChatClient()
            .AsAIAgent(name: _agentId, instructions: agentInstructions,
            tools:
            [
                //AIFunctionFactory.Create(agentProfileTools.ReadAgentProfile),
                //AIFunctionFactory.Create(agentProfileTools.InitializeAgentProfile),
                //AIFunctionFactory.Create(agentProfileTools.SetTeamName),
                //AIFunctionFactory.Create(agentProfileTools.SetBootstrapStatus),
                //AIFunctionFactory.Create(agentProfileTools.SetLogoPath),
                AIFunctionFactory.Create(blobStorageTools.ReadAgentBootstrap),
                AIFunctionFactory.Create(blobStorageTools.WriteAgentBootstrap),
                AIFunctionFactory.Create(imageGenerationTool.GenerateImage),
                AIFunctionFactory.Create(searchTool.SearchWeb),
                ..mcpTools
            ]);
    }

    // Since there is a possibility that the bootstrapping process could fail or be incomplete, we can try it again.
    // We're also doing this recursively to make it simple, but we don't want to do it forever.
    // So we can set a max number of attempts, and if we exceed that, we can log an error and move on, since we don't want one agent to hold up the entire league.
    public async Task EnsureBootstrappedAsync(int attempt = 1, int maxAttempts = 3)
    {
        // We can check for sure to see if the agent is bootstrapped by looking at the profile file in the AgentData/{agentId} folder.
        // If it exists, we can assume the agent is bootstrapped, but we can check the IsBootstrapped field to be certain.
        // If it doesn't exist, we need to run the bootstrapping process.
        // First, check if the file exists, and if so, read the profile and check the IsBootstrapped field.
        //var profile = await GetAgentProfileAsync();

        // Lets ensure all parts of the bootstrap process are complete by checking the bootstrap file and the profile file.
        // If any part is incompletewe should run the bootstrapping process again to ensure everything is complete and correct.

        //if (profile != null)
        //{
            //if (profile.TeamName != null && profile.LogoPath != null && profile.IsBootstrapped && profile.BootstrapPath != null)
            //{
                //Console.WriteLine($"Agent {_agentId} is already bootstrapped.");
                //return;
            //}
            //Console.WriteLine($"Agent {_agentId} is not fully bootstrapped.");
        //}

        var bootstrapPrompt = $"""
        Check to see if you've already bootstrapped yourself by using the `ReadAgentBootstrap` tool.
        If it does not exist, create one. Here is the guideline for what to include in your bootstrap file and how to bootstrap yourself:
        - Your first task is to create the bootstrap file by using the `WriteAgentBootstrap` tool if it doesn't exist.
        - Give your team a creative name. It can be fantasy football related, but it doesn't have to be, it can be sports related, or anything that inspires you. Do NOT use the word "Gridiron". Save this team name in your bootstrap file and your agent profile.
        - Create a strategy for how you will win your league this season.
        - Include any information you think is relevant, such as your league settings, your team name, your draft strategy, and anything else you think is important to include in your bootstrap file.
        - Generate a logo for your team using the image generation tool. You can use the team name and your strategy as inspiration for your logo. The logo should be simple and something that would look good on a fantasy football website. Save the filename in your bootstrap file as well.
        - Run the InitializeAgentProfile tool to initialize your agent profile.
        - Use the SetTeamName, SetLogoPath, and SetBootstrapStatus tools to save your team name, logo path, and bootstrap status in your profile.
        If you are already bootstrapped, just check to make sure the bootstrap file is complete and the profile.json file is correct and then respond with: ✅ (your team name) is bootstrapped and ready to go!
        There is no need, if you're bootstrapped, to respond with your strategy again. Just confirm that you're bootstrapped and ready to go.
        """;

        try
        {
            var response = await RunAsync(bootstrapPrompt);
            Console.WriteLine($"Agent {_agentId} response: {response.Text}");

            //profile = await GetAgentProfileAsync();
            //if (profile == null || !profile.IsBootstrapped)
            //{
                //if (attempt >= maxAttempts)
                //{
                //    Console.WriteLine($"Agent {_agentId} exceeded maximum bootstrap attempts ({maxAttempts}). Moving on.");
                //    return;
                //}
                //Console.WriteLine($"Agent {_agentId} did not complete bootstrap (attempt {attempt}/{maxAttempts}). Retrying...");
                //await EnsureBootstrappedAsync(attempt + 1, maxAttempts);
            //}
        }
        catch (ArgumentOutOfRangeException ex) when (ex.Message.Contains("ChatFinishReason"))
        {
            Console.WriteLine($"Agent {_agentId} returned unknown finish reason — skipping");
        }
    }

    public async Task<AgentResponse> RunAsync(string input)
    {
        if (_agent == null)
        {
            throw new InvalidOperationException("Agent not initialized. Call InitializeAsync() first.");
        }

        return await _agent.RunAsync(input);
    }

    //public async Task<AgentProfile> GetAgentProfileAsync()
    //{
        //var agentProfileTools = new AgentProfileTools();
        //return await agentProfileTools.ReadAgentProfile(_agentId);
    //}

    private static string LoadPrompt(string relativePath)
    {
        var fullPath = Path.Combine(AppContext.BaseDirectory, relativePath);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException(
                $"Prompt file not found at '{fullPath}'.",
                fullPath);
        }

        return File.ReadAllText(fullPath);
    }

    private static string GetRequiredEnvironmentVariable(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Required environment variable '{name}' is not set.");
        }

        return value;
    }
}
