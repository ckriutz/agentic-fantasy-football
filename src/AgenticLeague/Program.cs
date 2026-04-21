using System.Net.Http.Headers;
using System.Security.Cryptography;
using Microsoft.Agents.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

var host = builder.Build();
var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Program");

List<AIAgent> agents = new List<AIAgent>();

//var dsa = await new DataStatusAgent().CreateDataStatusAgentAsync();
var player1 = await new FantasyAgent().CreateFantasyAgentAsync("player-01", "x-ai/grok-4.20");
var player2 = await new FantasyAgent().CreateFantasyAgentAsync("player-02", "google/gemma-4-26b-a4b-it");
var player3 = await new FantasyAgent().CreateFantasyAgentAsync("player-03", "anthropic/claude-sonnet-4.6");
var player4 = await new FantasyAgent().CreateFantasyAgentAsync("player-04", "moonshotai/kimi-k2.6");
var player5 = await new FantasyAgent().CreateFantasyAgentAsync("player-05", "openai/gpt-5.4");
agents.Add(player1);
agents.Add(player2);
agents.Add(player3);
agents.Add(player4);
agents.Add(player5);

foreach(var agent in agents)
{
    var response = await agent.RunAsync("You're being initilized. Check to see if you're bootstrapped, and if not, begin that process. Respond back with your team name and quick summary of your strategy.");
    logger.LogInformation("Agent {agentName} response: {Response}", agent.Name, response);
}

foreach(var agent in agents)
{
    var response = await agent.RunAsync("Look at your roster and identify if you have room for additional players. If so, use the tools available to you to do research and find a player to add to your roster. Then use the tools to add that player to your roster. Select ONE player only. Respond with the name of the player you added and why you chose that player based on your strategy and team needs.");
    logger.LogInformation("Agent {agentName} response: {Response}", agent.Name, response);
}