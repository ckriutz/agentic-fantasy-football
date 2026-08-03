using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;

// OpenRouter proxies many upstream providers, and when one of them fails mid-request it still returns
// HTTP 200 with a body the OpenAI SDK cannot deserialize:
//   - "finish_reason": "error" (or another value outside stop/length/tool_calls/content_filter/function_call),
//     which makes ChatFinishReasonExtensions.ToChatFinishReason throw ArgumentOutOfRangeException and kill the process.
//   - a top-level "error" object with no usable choices.
// This handler normalizes those bodies before the SDK ever sees them so a single bad upstream response
// cannot take down the whole league run.
internal sealed class OpenRouterResponseNormalizingHandler : DelegatingHandler
{
    private static readonly HashSet<string> KnownFinishReasons = new(StringComparer.OrdinalIgnoreCase) { "stop", "length", "tool_calls", "content_filter", "function_call" };

    private readonly ILogger _logger;

    public OpenRouterResponseNormalizingHandler(ILogger logger) : base(new HttpClientHandler()) { _logger = logger; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);

        // Only JSON bodies are rewritten. Streaming (text/event-stream) and anything else passes straight through.
        if (response.Content?.Headers.ContentType?.MediaType != "application/json") { return response; }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(body)) { return response; }

        JsonNode? root;
        try { root = JsonNode.Parse(body); }
        catch (JsonException) { return ReplaceContent(response, body); }

        if (root is not JsonObject rootObject) { return ReplaceContent(response, body); }

        // An upstream failure surfaced as a top-level "error" is turned into a 503 so the existing
        // retry loop in FantasyAgent.RunAsync backs off and tries again instead of failing the agent.
        if (rootObject.TryGetPropertyValue("error", out var errorNode) && errorNode is not null)
        {
            _logger.LogWarning("OpenRouter returned an error payload; mapping to HTTP 503 so the run can retry. Payload: {Payload}", Truncate(errorNode.ToJsonString()));
            response.StatusCode = HttpStatusCode.ServiceUnavailable;
            return ReplaceContent(response, body);
        }

        if (!rootObject.TryGetPropertyValue("choices", out var choicesNode) || choicesNode is not JsonArray choices) { return ReplaceContent(response, body); }

        var rewritten = false;
        foreach (var choice in choices)
        {
            if (choice is not JsonObject choiceObject) { continue; }
            if (!choiceObject.TryGetPropertyValue("finish_reason", out var finishReasonNode)) { continue; }

            var finishReason = finishReasonNode?.GetValue<string?>();

            // A null finish_reason is valid (the SDK treats it as absent); only unknown non-null values crash it.
            if (finishReason is null || KnownFinishReasons.Contains(finishReason)) { continue; }

            _logger.LogWarning("OpenRouter returned unsupported finish_reason '{FinishReason}'; rewriting to 'stop'.", finishReason);
            choiceObject["finish_reason"] = "stop";
            rewritten = true;
        }

        return rewritten ? ReplaceContent(response, rootObject.ToJsonString()) : ReplaceContent(response, body);
    }

    // The original content stream has already been consumed, so it must be replaced with a fresh buffered copy.
    private static HttpResponseMessage ReplaceContent(HttpResponseMessage response, string body)
    {
        var originalContentType = response.Content?.Headers.ContentType;
        response.Content?.Dispose();
        response.Content = new StringContent(body, Encoding.UTF8);
        if (originalContentType is not null) { response.Content.Headers.ContentType = originalContentType; }
        else { response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json"); }
        return response;
    }

    private static string Truncate(string value) => value.Length <= 500 ? value : value.Substring(0, 500) + "... (truncated)";
}
