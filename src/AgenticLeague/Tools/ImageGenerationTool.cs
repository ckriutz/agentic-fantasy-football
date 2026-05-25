using System.ComponentModel;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

public sealed class ImageGenerationTool
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ImageGenerationTool> _logger;
    private readonly string _agentId;
    private readonly HttpClient _downloadHttpClient = new();
    private readonly BlobStorageTools _blobStorageTools = new BlobStorageTools();

    public ImageGenerationTool(string agentId, ILogger<ImageGenerationTool>? logger = null, string? rootPath = null)
    {
        _logger = logger ?? NullLogger<ImageGenerationTool>.Instance;
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(Environment.GetEnvironmentVariable("XAI_BASE_URL") ?? "https://api.x.ai/v1/")
        };

        var apiKey = GetRequiredEnvironmentVariable("XAI_API_KEY");
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        _agentId = GetSafeAgentId(agentId);
    }

    [Description("Generates an image from a text description and returns the filename of the image.")]
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

            using var createContent = new StringContent(JsonSerializer.Serialize(createRequest), Encoding.UTF8, new MediaTypeHeaderValue("application/json"));

            using var response = await _httpClient.PostAsync("images/generations", createContent);

            var responseBody = await response.Content.ReadAsStringAsync();
            _logger.LogDebug("POST /images/generations → {StatusCode} {StatusName}", (int)response.StatusCode, response.StatusCode);
            _logger.LogTrace("Response body: {ResponseBody}", responseBody);

            response.EnsureSuccessStatusCode();

            using var json = JsonDocument.Parse(responseBody);
            var url = json.RootElement.GetProperty("data")[0].GetProperty("url").GetString() ?? throw new InvalidOperationException("Response did not include an image URL.");

            _logger.LogInformation("Image generation complete. URL: {Url}", url);
            
            var fileName = await DownloadAndSaveImageToBlobStorageAsync(url);
            _logger.LogInformation("Image generation complete. Saved as {FileName}", fileName);
            return fileName;
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

    private static string GetSafeAgentId(string agentId)
    {
        if (string.IsNullOrWhiteSpace(agentId))
        {
            throw new ArgumentException("Agent ID is required.", nameof(agentId));
        }

        var safeAgentId = Regex.Replace(agentId.Trim(), @"[^a-zA-Z0-9\-_]", "");
        if (string.IsNullOrWhiteSpace(safeAgentId))
        {
            throw new InvalidOperationException("Agent ID must contain at least one valid character.");
        }

        return safeAgentId;
    }

    private async Task<string> DownloadAndSaveImageToBlobStorageAsync(string imageUrl)
    {
        using var response = await _downloadHttpClient.GetAsync(imageUrl, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
    
        var contentType = response.Content.Headers.ContentType?.MediaType;
        var extension = GetImageExtension(contentType);
        var fileName = $"logo{extension}";
    
        await using var imageStream = await response.Content.ReadAsStreamAsync();
        var uploadResult = await _blobStorageTools.UploadImageAsync(_agentId, fileName, imageStream);

        if (uploadResult == null)
        {
            _logger.LogError("Failed to upload image for agent {AgentId}.", _agentId);
            return null;
        }

        _logger.LogInformation("Saved generated image. Upload result: {UploadResult}", uploadResult);
        return fileName;
    }

    private static string GetImageExtension(string? contentType)
    {
        return contentType?.ToLowerInvariant() switch
        {
            "image/png" => ".png",
            "image/jpeg" => ".jpg",
            "image/jpg" => ".jpg",
            "image/webp" => ".webp",
            "image/gif" => ".gif",
            _ => ".png"
        };
    }
}
