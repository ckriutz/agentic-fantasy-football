using System.ClientModel;
using System.ComponentModel.DataAnnotations;
using System.Data.Common;
using AgenticLeague.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;

public class FantasyAgent
{
    private static readonly string endpoint =
        Environment.GetEnvironmentVariable("OPENROUTER_BASE_URL") ?? "https://openrouter.ai/api/v1";
    private static readonly string apiKey = GetRequiredEnvironmentVariable("OPENROUTER_API_KEY");
    public AIAgent? _agent;
    HttpClient httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:5000/") };

    public async Task<AIAgent> CreateFantasyAgentAsync(string agentId, string modelName)
    {
        var agentProfileTools = new AgentProfileTools();
        var bootstrapTools = new BootstrapTools();
        var imageGenerationTool = new ImageGenerationTool();
        
        var leaguePrompt = LoadPrompt("Agents/FantasyAgent.league.md");

        var agentInstructions =
        $"""
        You are {agentId}, a fantasy football manager, and your job is to manage your fantasy football team to victory.
        You are using the {modelName} model to help you make decisions and manage your team.
        Here are the fantasy football league rules and settings that you should be aware of:
        {leaguePrompt}
        
        First, check to see if you've already bootstrapped yourself by looking for the file Agents/{agentId}/bootstrap.md.
        If it does not exist, do these steps:
        - Your first task is to create the bootstrap.md file
        - Give your team a creative name, and create a strategy for how you will win your league this season.
        - Include any information you think is relevant, such as your league settings, your team name, your draft strategy, and anything else you think is important to include in your bootstrap file.
        - Generate a logo for your team using the image generation tool. You can use the team name and your strategy as inspiration for your logo. The logo should be simple and something that would look good on a fantasy football website. Save the logo URL in your bootstrap file as well.
        - Run the InitializeAgentProfile tool to initialize your agent profile.
        - Use the SetTeamName, SetLogoPath, and SetBootstrapStatus tools to save your team name, logo path, and bootstrap status in your profile.
        
        If the bootstrap file exists, read it to get up to speed on your current team, league, and any other relevant information.
        You can update this bootstrap file at any time to keep track of your evolving strategy and team information.
        """;

        _agent = new ChatClient(modelName, new ApiKeyCredential(apiKey),
            new OpenAIClientOptions {Endpoint = new Uri(endpoint)})
            .AsIChatClient()
            .AsAIAgent(name: "FantasyAgent", instructions: agentInstructions,
            tools:
            [
                AIFunctionFactory.Create(agentProfileTools.ReadAgentProfile),
                AIFunctionFactory.Create(agentProfileTools.InitializeAgentProfile),
                AIFunctionFactory.Create(agentProfileTools.SetTeamName),
                AIFunctionFactory.Create(agentProfileTools.SetBootstrapStatus),
                AIFunctionFactory.Create(agentProfileTools.SetLogoPath),
                AIFunctionFactory.Create(bootstrapTools.ReadAgentBootstrap),
                AIFunctionFactory.Create(bootstrapTools.WriteAgentBootstrap),
                AIFunctionFactory.Create(imageGenerationTool.GenerateImage)
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
