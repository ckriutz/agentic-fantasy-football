using System.ComponentModel;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;

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
                Console.WriteLine($"Bootstrap file uploaded successfully for agent '{agentId}'.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to upload bootstrap for agent '{agentId}'. Error: {ex.Message}");
            return $"Failed to upload bootstrap for agent '{agentId}'. Error: {ex.Message}";
        }
        

        if (!await blobClient.ExistsAsync())
        {
            Console.WriteLine($"Failed to upload bootstrap for agent '{agentId}'.");
            return $"Failed to upload bootstrap for agent '{agentId}'. Maybe try again?";
        }
        Console.WriteLine($"Bootstrap file uploaded successfully for agent '{agentId}'.");
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
            Console.WriteLine($"Bootstrap file not found for agent '{agentId}'.");
            return $"Bootstrap file not found for agent '{agentId}'. Maybe create and upload it first?";
        }

        var downloadInfo = await blobClient.DownloadAsync();
        using (var reader = new StreamReader(downloadInfo.Value.Content))
        {
            Console.WriteLine($"Bootstrap file read successfully for agent '{agentId}'.");
            return await reader.ReadToEndAsync();
        }
    }

    public async Task<System.Uri> UploadImageAsync(string agentId, string fileName, Stream stream)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
        BlobClient blobClient = containerClient.GetBlobClient($"{agentId}/{fileName}");
        Console.WriteLine($"Uploading image for agent '{agentId}' to blob '{agentId}/{fileName}'.");
        await blobClient.UploadAsync(stream, true);

        if (!await blobClient.ExistsAsync())
        {
            throw new InvalidOperationException($"Image upload did not create blob '{agentId}/{fileName}'.");
        }

        Console.WriteLine($"Image uploaded successfully for agent '{agentId}' to blob '{agentId}/{fileName}'.");
        return blobClient.Uri;
    }
}

//     [Description("Lists the blobs for the specified agent in Azure Blob Storage. Returns the blob name and a full URL for each item so the agent can read or reference the files.")]
//     public async Task<List<BlobInfo>> ListFilesAsync([Description("The agent ID, such as player-01.")] string agentId)
//     {
//         // We need to list all of the files for an agent in their specific folder in blob storage.
//         // This will allow the agent to know what files they have access to and read them as needed for their bootstrapping and operation.
//         var containerClient = _blobServiceClient.GetBlobContainerClient("agentdata");
//         var results = new List<BlobInfo>();

//         await foreach (var blobItem in containerClient.GetBlobsByHierarchyAsync(
//             BlobTraits.None,
//             BlobStates.All,
//             prefix: $"{agentId}/",
//             delimiter: "/",
//             cancellationToken: CancellationToken.None))
//         {
//             if (blobItem.IsBlob)
//             {
//                 var blobClient = containerClient.GetBlobClient(blobItem.Blob.Name);
//                 results.Add(new BlobInfo(blobItem.Blob.Name, blobClient.Uri.ToString()));
//             }
//         }

//         return results;
//     }
// }

/// <summary>
/// Represents a blob in Azure Storage with its name and a direct URL the agent can use to read or reference it.
/// </summary>
//public record BlobInfo(string Name, string Url);