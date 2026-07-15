using System.Text;
using System.Text.Json;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using SleeperSync.Models;

namespace SleeperSync.Services;

public sealed class SleeperSnapshotStorage(BlobServiceClient blobServiceClient, string blobContainerName)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly BlobServiceClient _blobServiceClient = blobServiceClient;
    private readonly string _blobContainerName = blobContainerName;

    public string ContainerName => _blobContainerName;

    public string GetBlobNameForUtc(DateTimeOffset utcNow)
    {
        var eastern = TimeZoneInfo.ConvertTime(utcNow, TimeZoneInfo.FindSystemTimeZoneById("America/New_York"));
        return $"sleeper/{eastern.Year:D4}/{eastern.Month:D2}/{eastern.Day:D2}/players.json";
    }

    public async Task<bool> ExistsAsync(string blobName, CancellationToken cancellationToken)
    {
        var blobClient = _blobServiceClient.GetBlobContainerClient(_blobContainerName).GetBlobClient(blobName);
        return await blobClient.ExistsAsync(cancellationToken);
    }

    public async Task<string> SaveAsync(SleeperPlayersSnapshot snapshot, CancellationToken cancellationToken)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(_blobContainerName);
        await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        var blobName = GetBlobNameForUtc(snapshot.RetrievedAtUtc);
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
