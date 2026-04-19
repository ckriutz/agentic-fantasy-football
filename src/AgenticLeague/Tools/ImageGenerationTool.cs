using System.ComponentModel;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

public sealed class ImageGenerationTool
{
    private readonly string _rootPath;
    private readonly HttpClient _httpClient;
    private readonly ILogger<ImageGenerationTool> _logger;

    public ImageGenerationTool(ILogger<ImageGenerationTool>? logger = null, string? rootPath = null)
    {
        _logger = logger ?? NullLogger<ImageGenerationTool>.Instance;
        _rootPath = rootPath ?? Directory.GetCurrentDirectory();
        _httpClient = new HttpClient();
        _httpClient.BaseAddress = new Uri(Environment.GetEnvironmentVariable("XAI_BASE_URL") ?? "https://api.x.ai/v1/");

        var apiKey = GetRequiredEnvironmentVariable("XAI_API_KEY");
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
    }

    [Description("Generates an image from a text description and returns the image URL.")]
    public async Task<string> GenerateImage([Description("The description of the image.")] string description)
    {
        try
        {
            _logger.LogInformation("Starting image generation for: {Description}", description);

            var createRequest = new
            {
                model = "grok-imagine-image",
                prompt = description
            };

            using var createContent = new StringContent(
                JsonSerializer.Serialize(createRequest), Encoding.UTF8,
                new MediaTypeHeaderValue("application/json"));

            using var response = await _httpClient.PostAsync("images/generations", createContent);

            var responseBody = await response.Content.ReadAsStringAsync();
            _logger.LogDebug("POST /images/generations → {StatusCode} {StatusName}", (int)response.StatusCode, response.StatusCode);
            _logger.LogTrace("Response body: {ResponseBody}", responseBody);

            response.EnsureSuccessStatusCode();

            using var json = JsonDocument.Parse(responseBody);
            var url = json.RootElement
                .GetProperty("data")[0]
                .GetProperty("url")
                .GetString()
                ?? throw new InvalidOperationException("Response did not include an image URL.");

            _logger.LogInformation("Image generation complete. URL: {Url}", url);
            return url;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Image generation failed for description: {Description}", description);
            throw;
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
