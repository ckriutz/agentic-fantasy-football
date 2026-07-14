using System.Text;
using System.Text.Json;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using FantasyProsDataSync.Models;

namespace FantasyProsDataSync.Services;

public sealed class FantasyProsSnapshotStorage(BlobServiceClient blobServiceClient, string blobContainerName)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly BlobServiceClient _blobServiceClient = blobServiceClient;
    private readonly string _blobContainerName = blobContainerName;

    public async Task<string> SaveAsync(FantasyProsPlayersSnapshot snapshot, CancellationToken cancellationToken)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(_blobContainerName);
        await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        var blobName = $"fantasypros/{snapshot.Season}/week-{snapshot.Week:D2}/players.json";
        var blobClient = containerClient.GetBlobClient(blobName);
        var contents = JsonSerializer.Serialize(snapshot, SerializerOptions);

        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(contents));
        await blobClient.UploadAsync(
            stream,
            new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = "application/json"
                }
            },
            cancellationToken);

        return blobName;
    }
}
