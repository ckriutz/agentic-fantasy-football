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
    public AIAgent? _agent;
    HttpClient httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:5000/") };
    private McpClient? _leagueApiMcpClient;

    public async Task<AIAgent> CreateFantasyAgentAsync(string agentId, string modelName)
    {
        var agentProfileTools = new AgentProfileTools();
        var bootstrapTools = new BootstrapTools();
        var imageGenerationTool = new ImageGenerationTool();
        var searchTool = new SearchTool();
        
        var leaguePrompt = LoadPrompt("Prompts/FantasyAgent.league.md");
        var howToPlayPrompt = LoadPrompt("Prompts/FantasyAgent.how-to-play.md");

        var transport = new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = new Uri("http://localhost:5000/mcp"),
            Name = "LeagueAPI"
        });

        _leagueApiMcpClient = await McpClient.CreateAsync(transport);
        IList<McpClientTool> mcpTools = await _leagueApiMcpClient.ListToolsAsync();

        var agentInstructions =
        $"""
        You are {agentId}, a fantasy football manager, and your job is to manage your fantasy football team to victory.
        You are using the {modelName} model to help you make decisions and manage your team.
        
        First, check to see if you've already bootstrapped yourself by looking for the file Agents/{agentId}/bootstrap.md.
        If it does not exist, create one. Here is the guideline for what to include in your bootstrap file and how to bootstrap yourself:
        - Your first task is to create the bootstrap.md file if it doesn't exist.
        - Give your team a creative name. It can be fantasy football related, but it doesn't have to be, it can be sports related, or anything that inspires you. Do NOT use the word "Gridiron". Save this team name in your bootstrap file and your agent profile.
        - Create a strategy for how you will win your league this season.
        - Include any information you think is relevant, such as your league settings, your team name, your draft strategy, and anything else you think is important to include in your bootstrap file.
        - Generate a logo for your team using the image generation tool. You can use the team name and your strategy as inspiration for your logo. The logo should be simple and something that would look good on a fantasy football website. Save the logo URL in your bootstrap file as well.
        - Run the InitializeAgentProfile tool to initialize your agent profile.
        - Use the SetTeamName, SetLogoPath, and SetBootstrapStatus tools to save your team name, logo path, and bootstrap status in your profile.
        
        If the bootstrap file exists, read it to get up to speed on your current team, league, and any other relevant information.
        You can update this bootstrap file at any time to keep track of your evolving strategy and team information. This is encouraged as the season goes on and you learn more about your team and the league.
        Just make sure to keep your profile updated with any changes you make to your bootstrap file.
        If the bootstrap file is missing a logo, generate one based on your team name and strategy and update the bootstrap file and your profile with the new logo information.

        With this information, here are the fantasy football league rules and settings that you should be aware of:
        {leaguePrompt}

        Here are instructions on how to play fantasy football and manage your team:
        {howToPlayPrompt}

        Use the SearchWeb tool whenever you need current external research about players, injuries, depth charts, rankings, or matchup context before making a move.
        """;

        _agent = new ChatClient(modelName, new ApiKeyCredential(apiKey),
            new OpenAIClientOptions {Endpoint = new Uri(endpoint)})
            .AsIChatClient()
            .AsAIAgent(name: agentId, instructions: agentInstructions,
            tools:
            [
                AIFunctionFactory.Create(agentProfileTools.ReadAgentProfile),
                AIFunctionFactory.Create(agentProfileTools.InitializeAgentProfile),
                AIFunctionFactory.Create(agentProfileTools.SetTeamName),
                AIFunctionFactory.Create(agentProfileTools.SetBootstrapStatus),
                AIFunctionFactory.Create(agentProfileTools.SetLogoPath),
                AIFunctionFactory.Create(bootstrapTools.ReadAgentBootstrap),
                AIFunctionFactory.Create(bootstrapTools.WriteAgentBootstrap),
                AIFunctionFactory.Create(imageGenerationTool.GenerateImage),
                AIFunctionFactory.Create(searchTool.SearchWeb),
                ..mcpTools
            ]);

        return _agent;
    }

    public async Task<AgentProfile> GetAgentProfileAsync(string agentId)
    {
        var agentProfileTools = new AgentProfileTools();
        return await agentProfileTools.ReadAgentProfile(agentId);
    }

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
