using System.ClientModel;
using ModelContextProtocol.Client;
using AgenticLeague.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Chat;

public class FantasyAgent
{

    private AIAgent? _agent;
    HttpClient httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:5000/") };
    private McpClient? _leagueApiMcpClient;
    private readonly AgentProfile _profile;
    private readonly ILogger<FantasyAgent> _logger;

    public FantasyAgent(AgentProfile profile, ILogger<FantasyAgent> logger)
    {
        _profile = profile;
        _logger = logger;
    }

    public string GetAgentName() => _profile.AgentId;

    public async Task InitializeAsync()
    {
        if (_agent != null) { return; } // Already initialized, no need to do it again.
        _agent = await GetAIAgentAsync(GetChatClient(_profile), _profile);
    }

    // Since there is a possibility that the bootstrapping process could fail or be incomplete, we can try it again.
    // We're also doing this recursively to make it simple, but we don't want to do it forever.
    // So we can set a max number of attempts, and if we exceed that, we can log an error and move on, since we don't want one agent to hold up the entire league.
    public async Task EnsureBootstrappedAsync(int attempt = 1, int maxAttempts = 3)
    {
        // Lets do a code-first check here before we run the agent, to see if the bootstrap file exists and is complete.
        // This will save us some time and API calls if the agent is already bootstrapped.
        // To be bootsrapped, the agent needs a bootstrap.md file, a logo.png file, and a team name in their agent profile.

        if (_profile.IsBootstrapped)
        {
            Console.WriteLine($"✅ {_profile.AgentId} is bootstrapped and ready to go!");
            return;
        }

        var bootstrapPrompt = $"Use the `agent-bootstrap` skill to bootstrap agent `{_profile.AgentId}`. No reason to use any other skills.";

        try
        {
            var response = await RunAsync(bootstrapPrompt);
            Console.WriteLine($"Agent {_profile.AgentId} response: {response.Text}");
        }
        catch (ArgumentOutOfRangeException ex) when (ex.Message.Contains("ChatFinishReason"))
        {
            Console.WriteLine($"Agent {_profile.AgentId} returned unknown finish reason — skipping");
        }
    }

    private IChatClient GetChatClient(AgentProfile aProfile)
    {
        if (aProfile.Connection == "OpenRouter")
        {
            var key = EnvironmentVariableHelper.GetRequired("OPENROUTER_API_KEY");
            OpenAIClientOptions options = new OpenAIClientOptions
            {
                Endpoint = new Uri("https://openrouter.ai/api/v1"),
                NetworkTimeout = TimeSpan.FromMinutes(5),
            };
            OpenAIClient openAIClient = new OpenAIClient(new ApiKeyCredential(key), options);

            #pragma warning disable OPENAI001
            var chatClient = openAIClient.GetResponsesClient().AsIChatClient(aProfile.ModelName);
            #pragma warning restore OPENAI001

            return chatClient;
        }
        if (aProfile.Connection == "MSFoundry")
        {
            var key = EnvironmentVariableHelper.GetRequired("FoundryKey");
            var foundryEndpoint = EnvironmentVariableHelper.GetRequired("FoundryEndpoint");
            OpenAIClientOptions options = new OpenAIClientOptions
            {
                Endpoint = new Uri(foundryEndpoint),
                NetworkTimeout = TimeSpan.FromMinutes(5),
            };
            OpenAIClient openAIClient = new OpenAIClient(new ApiKeyCredential(key), options);

            #pragma warning disable OPENAI001
            var chatClient = openAIClient.GetResponsesClient().AsIChatClient(aProfile.ModelName);
            #pragma warning restore OPENAI001

            return chatClient;
        }
        if (aProfile.Connection == "Meta")
        {
            var key = EnvironmentVariableHelper.GetRequired("META_API_KEY");
            OpenAIClientOptions options = new OpenAIClientOptions
            {
                Endpoint = new Uri("https://api.meta.ai/v1"),
                NetworkTimeout = TimeSpan.FromMinutes(5),
            };
            IChatClient chatClient = new ChatClient(aProfile.ModelName, new ApiKeyCredential(key), options).AsIChatClient();

            return chatClient;
        }
        throw new InvalidOperationException($"Unsupported connection type '{aProfile.Connection}'.");
    }

    private async Task<AIAgent> GetAIAgentAsync(IChatClient chatClient, AgentProfile aProfile)
    {
        var blobStorageTools = new BlobStorageTools();
        var imageGenerationTool = new ImageGenerationTool(aProfile.AgentId);
        var searchTool = new SearchTool();
        var howToPlayPrompt = await blobStorageTools.GetPromptFromBlobStorageAsync("FantasyAgent.how-to-play.md");

        // The LeaugeAPI has a LOT of tools as well, and this is how we get to them.
        var mcpTransport = new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = new Uri("http://localhost:5000/mcp"),
            Name = "LeagueAPI"
        });

        // Now, connect to the LeagueAPI MCP endpoint to get the tools we can use to interact with the league, such as viewing the draft board, making trades, adding players to our roster, etc.
        _leagueApiMcpClient = await McpClient.CreateAsync(mcpTransport);
        IList<McpClientTool> mcpTools = await _leagueApiMcpClient.ListToolsAsync();

        // Skills are copied to the output directory as Skills/ for normal runs.
        var skillsPath = Path.Combine(AppContext.BaseDirectory, "Skills");
        if (!Directory.Exists(skillsPath))
        {
            // Support local runs that have not copied project content to output yet.
            skillsPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Agents", "Skills"));
        }
        var skillsProvider = new AgentSkillsProvider(skillsPath);

        var agentInstructions =
        $"""
        You are {aProfile.AgentId}, a fantasy football manager, and your job is to manage your fantasy football team to victory.
        
        Your current team name, strategy, status, and memory can be found by using `ReadAgentBootstrap` tool to read your bootstrapping file. Always read this file before making any decisions.

        Here are instructions on how to play fantasy football and manage your team:
        {howToPlayPrompt}

        Use the `SearchWeb` tool whenever you need current external research about players, injuries, depth charts, rankings, or matchup context before making a move.
        Use the `ReadAgentBootstrap` and `WriteAgentBootstrap` tools to read and write your bootstrap file, which contains your strategy, team name, logo path, and bootstrap status.
        This is where you should keep any information about your team that you want to remember across interactions.
        """;

        AIAgent agent = chatClient.AsAIAgent(new ChatClientAgentOptions
        {
            Name = aProfile.AgentId,
            AIContextProviders = [skillsProvider],
            ChatOptions = new ChatOptions
            {
                Instructions = agentInstructions,
                Tools =
                [
                    AIFunctionFactory.Create(blobStorageTools.ReadAgentBootstrap),
                    AIFunctionFactory.Create(blobStorageTools.WriteAgentBootstrap),
                    AIFunctionFactory.Create(imageGenerationTool.GenerateImage),
                    AIFunctionFactory.Create(searchTool.SearchWeb),
                    ..mcpTools
                ]
            }
        });

        #pragma warning disable MAAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        agent = new AIAgentBuilder(agent)
        .Use(async (innerAgent, context, next, cancellationToken) =>
        {
            _logger.LogInformation(
                "→ {Function} args={Args}",
                context.Function.Name,
                context.Arguments);

            var result = await next(context, cancellationToken);

            _logger.LogInformation("← {Function}", context.Function.Name);
            return result;
        })
        .UseToolApproval(new ToolApprovalAgentOptions
        {
            AutoApprovalRules = [AgentSkillsProvider.AllToolsAutoApprovalRule],
        })
        .Build();
        #pragma warning restore MAAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        
        return agent;
    }

    public async Task<AgentResponse> RunAsync(string input)
    {
        if (_agent == null)
        {
            throw new InvalidOperationException("Agent not initialized. Call InitializeAsync() first.");
        }

        return await _agent.RunAsync(input);
    }

}
