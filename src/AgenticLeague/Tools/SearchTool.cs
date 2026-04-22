using System.ComponentModel;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

public sealed class SearchTool
{
    private static readonly string endpoint = Environment.GetEnvironmentVariable("OPENROUTER_BASE_URL") ?? "https://openrouter.ai/api/v1";
    private static readonly string apiKey = GetRequiredEnvironmentVariable("OPENROUTER_API_KEY");
    private static readonly string modelName = "perplexity/sonar";
    private readonly HttpClient _httpClient;

    public SearchTool()
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri($"{endpoint.TrimEnd('/')}/")
        };
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _httpClient.DefaultRequestHeaders.Add("HTTP-Referer", "https://github.com/ckriutz/agentic-fantasy-football");
        _httpClient.DefaultRequestHeaders.Add("X-Title", "Agentic Fantasy Football");
    }

    [Description("Researches fantasy football questions on the web using OpenRouter's Perplexity Sonar model. Use this for current player news, injuries, depth charts, rankings, and matchup context.")]
    public async Task<string> SearchWeb([Description("The research question to ask, including player names and the specific fantasy football decision you are trying to make.")] string query)
    {
        Console.WriteLine($"SearchTool received query: {query}");
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new ArgumentException("A search query is required.", nameof(query));
        }

        var request = new
        {
            model = modelName,
            messages = new object[]
            {
                new
                {
                    role = "system",
                    content = "You are a fantasy football research assistant. Provide concise, current, web-backed answers that help with player evaluation and roster decisions. Focus on actionable takeaways."
                },
                new
                {
                    role = "user",
                    content = query.Trim()
                }
            }
        };

        using var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

        using var response = await _httpClient.PostAsync("chat/completions", content);
        var responseBody = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"OpenRouter search request failed with status code {(int)response.StatusCode} ({response.StatusCode}). Response: {responseBody}");
        }

        using var responseJson = JsonDocument.Parse(responseBody);
        var answer = ExtractAnswer(responseJson.RootElement);
        var citations = ExtractCitations(responseJson.RootElement);

        if (citations.Count == 0)
        {
            return answer;
        }

        var sources = string.Join(Environment.NewLine, citations.Select(url => $"- {url}"));
        return $"{answer}{Environment.NewLine}{Environment.NewLine}Sources:{Environment.NewLine}{sources}";
    }

    private static string ExtractAnswer(JsonElement root)
    {
        if (!root.TryGetProperty("choices", out var choices) ||
            choices.ValueKind != JsonValueKind.Array ||
            choices.GetArrayLength() == 0)
        {
            throw new InvalidOperationException("OpenRouter response did not include any choices.");
        }

        var message = choices[0].GetProperty("message");
        if (!message.TryGetProperty("content", out var content))
        {
            throw new InvalidOperationException("OpenRouter response did not include message content.");
        }

        if (content.ValueKind == JsonValueKind.String)
        {
            var text = content.GetString();
            return !string.IsNullOrWhiteSpace(text)
                ? text.Trim()
                : throw new InvalidOperationException("OpenRouter returned an empty response.");
        }

        if (content.ValueKind == JsonValueKind.Array)
        {
            var textParts = content
                .EnumerateArray()
                .Select(part => part.TryGetProperty("text", out var textElement) ? textElement.GetString() : null)
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .ToArray();

            if (textParts.Length > 0)
            {
                return string.Join(Environment.NewLine, textParts).Trim();
            }
        }

        throw new InvalidOperationException("OpenRouter returned message content in an unexpected format.");
    }

    private static IReadOnlyList<string> ExtractCitations(JsonElement root)
    {
        var citations = new List<string>();

        AddCitationUrls(root, "citations", citations);

        if (root.TryGetProperty("choices", out var choices) &&
            choices.ValueKind == JsonValueKind.Array &&
            choices.GetArrayLength() > 0 &&
            choices[0].TryGetProperty("message", out var message))
        {
            AddCitationUrls(message, "citations", citations);
            AddCitationUrls(message, "annotations", citations);
        }

        return citations
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void AddCitationUrls(JsonElement element, string propertyName, ICollection<string> citations)
    {
        if (!element.TryGetProperty(propertyName, out var citationElement) || citationElement.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var item in citationElement.EnumerateArray())
        {
            switch (item.ValueKind)
            {
                case JsonValueKind.String:
                    AddCitation(item.GetString(), citations);
                    break;
                case JsonValueKind.Object:
                    if (item.TryGetProperty("url", out var url))
                    {
                        AddCitation(url.GetString(), citations);
                    }
                    else if (item.TryGetProperty("source", out var source))
                    {
                        AddCitation(source.GetString(), citations);
                    }
                    break;
            }
        }
    }

    private static void AddCitation(string? value, ICollection<string> citations)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            citations.Add(uri.ToString());
        }
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
