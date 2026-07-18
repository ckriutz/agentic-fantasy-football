using System.Text.Json;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using YahooDataSync.Configuration;
using YahooDataSync.Models;

namespace YahooDataSync.Services;

internal sealed class YahooSnapshotStorage(BlobServiceClient blobServiceClient, YahooStorageOptions storageOptions)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly BlobContainerClient _containerClient = blobServiceClient.GetBlobContainerClient(storageOptions.ContainerName);

    internal string ContainerName => _containerClient.Name;

    internal async Task<string> SaveAsync(YahooPlayersSnapshot snapshot, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        await _containerClient.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);

        var timestamp = snapshot.RetrievedAtUtc.UtcDateTime.ToString("yyyyMMdd'T'HHmmssfffffff'Z'", System.Globalization.CultureInfo.InvariantCulture);
        var blobName = $"yahoo/{snapshot.Season}/week-{snapshot.Week:D2}/{timestamp}-players.json";
        var options = new BlobUploadOptions
        {
            Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All },
            HttpHeaders = new BlobHttpHeaders { ContentType = "application/json" }
        };

        await _containerClient.GetBlobClient(blobName).UploadAsync(BinaryData.FromObjectAsJson(snapshot, SerializerOptions), options, cancellationToken);
        return blobName;
    }
}
