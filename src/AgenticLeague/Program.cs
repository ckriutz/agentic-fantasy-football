using System.Net.Http.Headers;
using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

var host = builder.Build();
var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Program");

//var dsa = await new DataStatusAgent().CreateDataStatusAgentAsync();
//var player1 = await new FantasyAgent().CreateFantasyAgentAsync("player-01", "x-ai/grok-4.1-fast");
//var player1Response = await player1.RunAsync("You're being initilized. Check to see if you're bootstrapped, and if not, begin that process. Respond back with your team name and quick summary of your strategy.");
//Console.WriteLine(player1Response);

//var play

//AgenticLeague.Models.AgentProfile? player2Profile = new AgenticLeague.Models.AgentProfile
//{
    //AgentId = "player-02",
    //ModelName = "google/gemma-4-31b-it",
//};
//var player2 = await new FantasyAgent().CreateFantasyAgentAsync(player2Profile);
//var player2Response = await player2.RunAsync("You're being initilized. Check to see if you're bootstrapped, and if not, begin that process. Respond back with your team name and quick summary of your strategy.");
//Console.WriteLine(player2Response);

var player3 = await new FantasyAgent().CreateFantasyAgentAsync("player-03", "x-ai/grok-4.1-fast");
var player3Response = await player3.RunAsync("You're being initilized. Check to see if you're bootstrapped, and if not, begin that process. Respond back with your team name and quick summary of your strategy.");
Console.WriteLine(player3Response);