using System.ComponentModel;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Microsoft.Extensions.Logging;

// This class provides tools for interacting with Azure Blob Storage to manage markdown and image data for the agents.
// Right now, we're only using it for the bootstrap.md file, and the logo, but it can be extended to manage any files the agents need to read or write as part of their operation.
// This will include methods for uploading, downloading, and listing files in a specified blob container.
// This allows the agents to access necessary data for their initialization and bootstrapping processes without relying on local file storage, making it more scalable and suitable for distributed environments.
// Some of these methods are used by the agents, and some are not.

public sealed class BlobStorageTools
{
    private readonly string _connectionString = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING") ?? throw new InvalidOperationException("Azure Storage connection string is not set in environment variables.");
    private BlobServiceClient _blobServiceClient;
    private readonly string _containerName = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONTAINER_NAME") ?? "agentdata";
    private ILogger<BlobStorageTools>? _logger;
    public BlobStorageTools()
    {
        // Initialize Azure Blob Storage client here using connection string and container name from environment variables.
        _blobServiceClient = new BlobServiceClient(_connectionString);
    }

    [Description("Creates or updates the agent's bootstrap markdown file.")]
    public async Task<string> WriteAgentBootstrap([Description("The agent ID, such as player-01.")] string agentId, [Description("The markdown content to write.")] string content)
    {
        string fileName = "bootstrap.md";
        var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
        BlobClient blobClient = containerClient.GetBlobClient($"{agentId}/{fileName}");

        try
        {
            using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content)))
            {
                await blobClient.UploadAsync(stream, true);
                _logger?.LogInformation($"Bootstrap file uploaded successfully for agent '{agentId}'.");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Failed to upload bootstrap for agent '{agentId}'. Error: {ex.Message}");
            return $"Failed to upload bootstrap for agent '{agentId}'. Error: {ex.Message}";
        }
        

        if (!await blobClient.ExistsAsync())
        {
            _logger?.LogError($"Failed to upload bootstrap for agent '{agentId}'.");
            return $"Failed to upload bootstrap for agent '{agentId}'. Maybe try again?";
        }
        _logger?.LogInformation($"Bootstrap file uploaded successfully for agent '{agentId}'.");
        return "Bootstrap uploaded successfully.";
    }

    [Description("Reads the agent's bootstrap markdown file.")]
    public async Task<string> ReadAgentBootstrap([Description("The agent ID, such as player-01.")] string agentId)
    {

        string fileName = "bootstrap.md";
        var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
        BlobClient blobClient = containerClient.GetBlobClient($"{agentId}/{fileName}");

        if (!await blobClient.ExistsAsync())
        {
            _logger?.LogWarning($"Bootstrap file not found for agent '{agentId}'.");
            return $"Bootstrap file not found for agent '{agentId}'. Maybe create and upload it first?";
        }

        var downloadInfo = await blobClient.DownloadAsync();
        using (var reader = new StreamReader(downloadInfo.Value.Content))
        {
            _logger?.LogInformation($"Bootstrap file read successfully for agent '{agentId}'.");
            return await reader.ReadToEndAsync();
        }
    }

    public async Task<System.Uri> UploadImageAsync(string agentId, string fileName, Stream stream)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
        BlobClient blobClient = containerClient.GetBlobClient($"{agentId}/{fileName}");
        _logger?.LogInformation($"Uploading image for agent '{agentId}' to blob '{agentId}/{fileName}'.");
        await blobClient.UploadAsync(stream, true);

        if (!await blobClient.ExistsAsync())
        {
            throw new InvalidOperationException($"Image upload did not create blob '{agentId}/{fileName}'.");
        }

        _logger?.LogInformation($"Image uploaded successfully for agent '{agentId}' to blob '{agentId}/{fileName}'.");
        return blobClient.Uri;
    }

    // This is a utility method to see if there is a bootstrap file for an agent in blob storage.
    public bool IsBootstrapFilePresent(string agentId)
    {
        string fileName = "bootstrap.md";
        var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
        BlobClient blobClient = containerClient.GetBlobClient($"{agentId}/{fileName}");
        return blobClient.Exists();
    }

    // This is another utility method to see if there is a logo file for an agent in blob storage.
    public bool IsLogoFilePresent(string agentId)
    {
        string fileName = "logo.png";
        var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
        BlobClient blobClient = containerClient.GetBlobClient($"{agentId}/{fileName}");
        return blobClient.Exists();
    }

    public async Task<string> GetPromptFromBlobStorageAsync(string promptName)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
        BlobClient blobClient = containerClient.GetBlobClient($"prompts/{promptName}");

        if (!await blobClient.ExistsAsync())
        {
            _logger?.LogWarning($"Blob 'prompts/{promptName}' not found.");
            return $"Blob 'prompts/{promptName}' not found.";
        }

        var downloadInfo = await blobClient.DownloadAsync();
        using (var reader = new StreamReader(downloadInfo.Value.Content))
        {
            _logger?.LogInformation($"Blob 'prompts/{promptName}' read successfully.");
            return await reader.ReadToEndAsync();
        }
    }
}
