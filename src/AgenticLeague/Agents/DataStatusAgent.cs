using System.ClientModel;
using System.ComponentModel;
using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.VisualBasic;
using OpenAI;

//using OpenAI;
using OpenAI.Chat;

public class DataStatusAgent
{
    private static readonly string endpoint =
        Environment.GetEnvironmentVariable("OPENROUTER_BASE_URL") ?? "https://openrouter.ai/api/v1";
    private static readonly string apiKey = GetRequiredEnvironmentVariable("OPENROUTER_API_KEY");
    public static string modelName = "google/gemma-4-31b-it";

    HttpClient httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:5000/") };

    public async Task<AIAgent> CreateDataStatusAgentAsync()
    {
        var dataSyncInfoTools = new DataSyncInfoTools();

        var agentInstructions =
        """
        You are a simple agent that checks the current status of the data pipeline.
        You're ready to answer questions about when Sleeper last ran, or when SportsDataIO last updated, or when Yahoo was last updated.
        You can also answer questions about the current status of the data pipeline, such as if there are any known issues or if everything is running smoothly.
        You're generally going to be used by other agents, so you should be concise and to the point with your answers.
        """;

        var agent = new ChatClient(modelName, new ApiKeyCredential(apiKey),
            new OpenAIClientOptions {Endpoint = new Uri(endpoint)})
            .AsIChatClient()
            .AsAIAgent(name: "DataStatusAgent", instructions: agentInstructions,
            tools: [
                AIFunctionFactory.Create(dataSyncInfoTools.CheckYahooStatus),
                AIFunctionFactory.Create(dataSyncInfoTools.CheckSleeperStatus),
                AIFunctionFactory.Create(dataSyncInfoTools.CheckSportsDataIOStatus)
            ]);
        return agent;
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
