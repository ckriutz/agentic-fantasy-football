using System.ClientModel;
using System.Text.Json;
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
    private readonly HttpClient _httpClient;
    private McpClient? _leagueApiMcpClient;
    private readonly AgentProfile _profile;
    private readonly ILogger<FantasyAgent> _logger;

    // Result summaries can be huge (e.g. get_available_players), so cap what we log/keep.
    private const int MaxResultLength = 2000;

    // Holds the trace for the RunAsync call currently executing on this async flow.
    // AsyncLocal keeps it scoped to the in-flight run so the tool middleware can record calls
    // without threading extra state through every tool invocation.
    private readonly AsyncLocal<RunTrace?> _currentRun = new();

    // A lightweight accumulator for one run: a stable id plus the tool calls made so far.
    private sealed class RunTrace
    {
        public string RunId { get; } = Guid.NewGuid().ToString();
        public List<AgentToolCall> ToolCalls { get; } = new();

        // Next 1-based sequence number for a tool call in this run.
        public int NextSequence => ToolCalls.Count + 1;
    }

    public FantasyAgent(AgentProfile profile, ILogger<FantasyAgent> logger, HttpClient httpClient)
    {
        _profile = profile;
        _logger = logger;
        _httpClient = httpClient;
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
            var response = (await RunAsync(bootstrapPrompt)).Response;
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
            Endpoint = new Uri($"{_httpClient.BaseAddress}mcp"),
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
        This bootstrapping file is where you should keep any information about your team that you want to remember across interactions.
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
        var agentId = aProfile.AgentId;
        agent = new AIAgentBuilder(agent)
        .Use(async (innerAgent, context, next, cancellationToken) =>
        {
            // Grab the trace for the current run (null if this call happens outside RunAsync).
            var run = _currentRun.Value;
            var argsText = FormatArguments(context.Arguments);

            // Log the outgoing tool request in a readable form.
            _logger.LogInformation("Agent {AgentId} → {Function} args={Args}", agentId, context.Function.Name, argsText);

            // Start building the record for this call.
            var toolCall = new AgentToolCall
            {
                RunId = run?.RunId ?? string.Empty,
                AgentId = agentId,
                Sequence = run?.NextSequence ?? 0,
                ToolName = context.Function.Name,
                Arguments = argsText,
                StartedAtUtc = DateTimeOffset.UtcNow,
            };

            try
            {
                var result = await next(context, cancellationToken);

                // The call returned - record timing, status, and a decoded/truncated view of the result.
                toolCall.CompletedAtUtc = DateTimeOffset.UtcNow;
                toolCall.Status = AgentToolCallStatus.Completed;
                toolCall.ResultSummary = FormatResult(result);

                _logger.LogInformation(
                    "Agent {AgentId} ← {Function} status={Status} durationMs={DurationMs} result={Result}",
                    agentId, context.Function.Name, toolCall.Status, toolCall.DurationMs, toolCall.ResultSummary);

                run?.ToolCalls.Add(toolCall);
                return result;
            }
            catch (Exception ex)
            {
                // The invocation threw before returning - capture it as a failed call so the trace stays complete.
                toolCall.CompletedAtUtc = DateTimeOffset.UtcNow;
                toolCall.Status = AgentToolCallStatus.Failed;
                toolCall.ErrorMessage = ex.Message;

                _logger.LogWarning(
                    "Agent {AgentId} ✗ {Function} failed after {DurationMs}ms: {Error}",
                    agentId, context.Function.Name, toolCall.DurationMs, ex.Message);

                run?.ToolCalls.Add(toolCall);
                throw;
            }
        })
        .UseToolApproval(new ToolApprovalAgentOptions
        {
            AutoApprovalRules = [AgentSkillsProvider.AllToolsAutoApprovalRule],
        })
        .Build();
        #pragma warning restore MAAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        
        return agent;
    }

    public async Task<AgentRunResult> RunAsync(string input)
    {
        if (_agent == null)
        {
            throw new InvalidOperationException("Agent not initialized. Call InitializeAsync() first.");
        }

        // Begin a fresh trace so the tool middleware records every call made during this run.
        var run = new RunTrace();
        _currentRun.Value = run;

        var retryDelays = new[] { TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(120) };
        try
        {
            for (var attempt = 0; attempt <= retryDelays.Length; attempt++)
            {
                try
                {
                    var response = await _agent.RunAsync(input);

                    // One summary line per run makes empty responses and tool volume obvious at a glance.
                    _logger.LogInformation(
                        "Agent run {RunId} for {AgentId} completed. ResponseTextEmpty={Empty}; ToolCallCount={Count}.",
                        run.RunId, _profile.AgentId, string.IsNullOrWhiteSpace(response?.Text), run.ToolCalls.Count);

                    return new AgentRunResult
                    {
                        RunId = run.RunId,
                        Response = response ?? new AgentResponse(),
                        ToolCalls = run.ToolCalls,
                    };
                }
                catch (ClientResultException ex) when (ex.Status == 429 || ex.Status == 503)
                {
                    if (attempt == retryDelays.Length)
                    {
                        _logger.LogError(ex, "Agent {AgentId} received HTTP {Status} after {AttemptCount} attempts. Moving on.", _profile.AgentId, ex.Status, attempt + 1);
                        return new AgentRunResult { RunId = run.RunId, Response = new AgentResponse(), ToolCalls = run.ToolCalls };
                    }

                    var retryDelay = retryDelays[attempt];
                    _logger.LogWarning(ex, "Agent {AgentId} received HTTP {Status} on attempt {Attempt}. Retrying in {RetryDelaySeconds} seconds.", _profile.AgentId, ex.Status, attempt + 1, retryDelay.TotalSeconds);
                    await Task.Delay(retryDelay);
                }
            }

            throw new InvalidOperationException("Agent execution ended without a response.");
        }
        finally
        {
            // Clear the trace so it can't leak into a later run on this async flow.
            _currentRun.Value = null;
        }
    }

    // Renders tool arguments as a compact "name=value, name=value" string rather than the
    // default dictionary formatting, which is easier to read in the logs.
    private static string FormatArguments(IEnumerable<KeyValuePair<string, object?>>? arguments)
    {
        if (arguments is null) { return string.Empty; }
        return string.Join(", ", arguments.Select(kvp => $"{kvp.Key}={kvp.Value}"));
    }

    // Turns a raw tool result into a readable, size-bounded string for logging/tracing.
    private static string FormatResult(object? result)
    {
        if (result is null) { return string.Empty; }

        // Strings (e.g. SearchWeb output) can be logged directly.
        // Otherwise, prefer unwrapping a root "Text" value before falling back to full serialization.
        var text = result as string
            ?? TryUnwrapText(result)
            ?? SafeSerialize(result);

        return Truncate(text, MaxResultLength);
    }

    // Many MCP/function results arrive as an object whose "Text" property already contains JSON.
    // If we re-serialize the whole object, those inner quotes get double-escaped (the old \u0022 noise),
    // so instead we detect that root "Text" property and return its decoded value directly.
    private static string? TryUnwrapText(object result)
    {
        try
        {
            var json = JsonSerializer.Serialize(result);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("Text", out var textProp)
                && textProp.ValueKind == JsonValueKind.String)
            {
                return textProp.GetString();
            }
        }
        catch
        {
            // Not unwrappable - let the caller fall back to full serialization.
        }

        return null;
    }

    // Best-effort JSON serialization, falling back to ToString() if the type can't be serialized.
    private static string SafeSerialize(object result)
    {
        try { return JsonSerializer.Serialize(result); }
        catch { return result.ToString() ?? string.Empty; }
    }

    // Caps very long values so a single tool result can't flood the logs.
    private static string Truncate(string value, int max)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= max) { return value; }
        return value.Substring(0, max) + "... (truncated)";
    }

}
