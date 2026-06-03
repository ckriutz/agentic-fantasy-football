using System.ComponentModel;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

public sealed class ImageGenerationTool
{
    private readonly ILogger<ImageGenerationTool> _logger;
    private readonly string _agentId;
    private readonly BlobStorageTools _blobStorageTools = new BlobStorageTools();
    private static readonly string openRouterEndpoint = Environment.GetEnvironmentVariable("OPENROUTER_BASE_URL") ?? "https://openrouter.ai/api/v1";
    private const string OpenRouterImageModel = "microsoft/mai-image-2.5";

    public ImageGenerationTool(string agentId, ILogger<ImageGenerationTool>? logger = null, string? rootPath = null)
    {
        _logger = logger ?? NullLogger<ImageGenerationTool>.Instance;
        _agentId = GetSafeAgentId(agentId);
    }

    [Description("Generates an image from a text description and returns the filename of the image.")]
    public async Task<string> GenerateImage([Description("The description of the image.")] string description)
    {
        return await GenerateMAIImage(description);
    }

    // If we decide to use xAI's image generation in the future, we can easily switch to it by changing this method to call GenerateXAIImage instead of GenerateMAIImage.
    // This uses the xAI API endpoint. We need to have the XAI_API_KEY and XAI_BASE_URL environment variables set for this to work.
    // The response from xAI includes a URL to the generated image, which we then download and save to blob storage, and return the filename.
    private async Task<string> GenerateXAIImage(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return "An image description is required. Try again with a description of the image you want.";
            //throw new ArgumentException("An image description is required.", nameof(description));
        }

        try
        {
            _logger.LogInformation("Starting xAI image generation for: {Description}", description);

            var createRequest = new
            {
                model = "grok-imagine-image-quality",
                prompt = description
            };

            using var xaiHttpClient = CreateXAIHttpClient();
            using var createContent = new StringContent(JsonSerializer.Serialize(createRequest), Encoding.UTF8, new MediaTypeHeaderValue("application/json"));

            using var response = await xaiHttpClient.PostAsync("images/generations", createContent);

            var responseBody = await response.Content.ReadAsStringAsync();
            _logger.LogDebug("POST /images/generations → {StatusCode} {StatusName}", (int)response.StatusCode, response.StatusCode);
            _logger.LogTrace("Response body: {ResponseBody}", responseBody);

            response.EnsureSuccessStatusCode();
            using var json = JsonDocument.Parse(responseBody);
            var url = json.RootElement.GetProperty("data")[0].GetProperty("url").GetString() ?? throw new InvalidOperationException("Response did not include an image URL.");

            _logger.LogInformation("xAI image generation complete. URL: {Url}", url);
            var fileName = await DownloadAndSaveImageToBlobStorageAsync(url, xaiHttpClient);
            _logger.LogInformation("xAI image generation complete. Saved as {FileName}", fileName);
            return fileName;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "xAI image generation failed for description: {Description}", description);
            throw;
        }
    }

    private async Task<string> GenerateMAIImage(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return "An image description is required. Try again with a description of the image you want.";
            //throw new ArgumentException("An image description is required.", nameof(description));
        }

        try
        {
            _logger.LogInformation("Starting MAI image generation for: {Description}", description);

            var request = new
            {
                model = OpenRouterImageModel,
                messages = new object[]
                {
                    new
                    {
                        role = "user",
                        content = description.Trim()
                    }
                },
                modalities = new[] { "image" }
            };

            using var openRouterHttpClient = CreateOpenRouterHttpClient();
            using var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, new MediaTypeHeaderValue("application/json"));
            using var response = await openRouterHttpClient.PostAsync("chat/completions", content);
            var responseBody = await response.Content.ReadAsStringAsync();
            _logger.LogDebug("POST /chat/completions → {StatusCode} {StatusName}", (int)response.StatusCode, response.StatusCode);
            _logger.LogTrace("OpenRouter response body: {ResponseBody}", responseBody);

            if (!response.IsSuccessStatusCode)
            {
                return $"Image generation failed with status code {(int)response.StatusCode} ({response.StatusCode}). Response: {responseBody}";
                //throw new InvalidOperationException($"OpenRouter MAI image request failed with status code {(int)response.StatusCode} ({response.StatusCode}). Response: {responseBody}");
            }

            using var json = JsonDocument.Parse(responseBody);
            var imageDataUrl = ExtractOpenRouterImageDataUrl(json.RootElement);
            var image = DecodeBase64Image(imageDataUrl);
            await using var imageStream = new MemoryStream(image.Bytes);
            var fileName = await SaveImageToBlobStorageAsync(imageStream, image.ContentType);
            _logger.LogInformation("MAI image generation complete. Saved as {FileName}", fileName);
            return fileName;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MAI image generation failed for description: {Description}", description);
            return $"Image generation failed: {ex.Message}";
            throw;
        }
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

    private static HttpClient CreateXAIHttpClient()
    {
        var apiKey = EnvironmentVariableHelper.GetRequired("XAI_API_KEY");
        var httpClient = new HttpClient
        {
            BaseAddress = new Uri(Environment.GetEnvironmentVariable("XAI_BASE_URL") ?? "https://api.x.ai/v1/")
        };
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return httpClient;
    }

    private static HttpClient CreateOpenRouterHttpClient()
    {
        var apiKey = EnvironmentVariableHelper.GetRequired("OPENROUTER_API_KEY");
        var httpClient = new HttpClient
        {
            BaseAddress = new Uri($"{openRouterEndpoint.TrimEnd('/')}/")
        };
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        httpClient.DefaultRequestHeaders.Add("HTTP-Referer", "https://github.com/ckriutz/agentic-fantasy-football");
        httpClient.DefaultRequestHeaders.Add("X-Title", "Agentic Fantasy Football");
        return httpClient;
    }

    private static string ExtractOpenRouterImageDataUrl(JsonElement root)
    {
        if (!root.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() == 0)
        {
            throw new InvalidOperationException("OpenRouter response did not include any choices.");
        }

        if (!choices[0].TryGetProperty("message", out var message) || !message.TryGetProperty("images", out var images) || images.ValueKind != JsonValueKind.Array || images.GetArrayLength() == 0)
        {
            throw new InvalidOperationException("OpenRouter response did not include any generated images.");
        }

        var image = images[0];
        if (TryGetImageUrl(image, "image_url", out var imageDataUrl) || TryGetImageUrl(image, "imageUrl", out imageDataUrl))
        {
            return imageDataUrl;
        }

        throw new InvalidOperationException("OpenRouter generated image did not include image URL data.");
    }

    private static bool TryGetImageUrl(JsonElement image, string propertyName, out string imageUrl)
    {
        imageUrl = string.Empty;
        if (!image.TryGetProperty(propertyName, out var imageUrlElement))
        {
            return false;
        }

        if (imageUrlElement.ValueKind == JsonValueKind.String)
        {
            imageUrl = imageUrlElement.GetString() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(imageUrl);
        }

        if (imageUrlElement.ValueKind == JsonValueKind.Object && imageUrlElement.TryGetProperty("url", out var urlElement) && urlElement.ValueKind == JsonValueKind.String)
        {
            imageUrl = urlElement.GetString() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(imageUrl);
        }

        return false;
    }

    private static (byte[] Bytes, string? ContentType) DecodeBase64Image(string imageData)
    {
        if (string.IsNullOrWhiteSpace(imageData))
        {
            throw new InvalidOperationException("OpenRouter returned empty image data.");
        }

        var base64Image = imageData.Trim();
        string? contentType = null;

        if (base64Image.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            var commaIndex = base64Image.IndexOf(',');
            if (commaIndex < 0)
            {
                throw new InvalidOperationException("OpenRouter returned an invalid image data URL.");
            }

            var metadata = base64Image[..commaIndex];
            if (!metadata.Contains(";base64", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("OpenRouter returned image data that is not base64 encoded.");
            }

            contentType = metadata[5..].Split(';', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            base64Image = base64Image[(commaIndex + 1)..];
        }

        try
        {
            return (Convert.FromBase64String(base64Image), contentType);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException("OpenRouter returned image data that is not valid base64.", ex);
        }
    }

    private async Task<string> DownloadAndSaveImageToBlobStorageAsync(string imageUrl, HttpClient httpClient)
    {
        _logger.LogInformation("Downloading generated image for agent {AgentId} from {ImageUrl}", _agentId, imageUrl);
        using var response = await httpClient.GetAsync(imageUrl, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
    
        var contentType = response.Content.Headers.ContentType?.MediaType;
        var extension = GetImageExtension(contentType);
        var fileName = $"logo{extension}";
        _logger.LogInformation("Downloaded generated image for agent {AgentId}. Content-Type: {ContentType}. Saving as {FileName}", _agentId, contentType ?? "unknown", fileName);
    
        await using var imageStream = await response.Content.ReadAsStreamAsync();
        var uploadResult = await _blobStorageTools.UploadImageAsync(_agentId, fileName, imageStream);
        _logger.LogInformation("Saved generated image. Upload result: {UploadResult}", uploadResult);
        return fileName;
    }

    private async Task<string> SaveImageToBlobStorageAsync(Stream imageStream, string? contentType)
    {
        var extension = GetImageExtension(contentType);
        var fileName = $"logo{extension}";
        _logger.LogInformation("Saving generated image for agent {AgentId}. Content-Type: {ContentType}. Saving as {FileName}", _agentId, contentType ?? "unknown", fileName);

        var uploadResult = await _blobStorageTools.UploadImageAsync(_agentId, fileName, imageStream);
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
