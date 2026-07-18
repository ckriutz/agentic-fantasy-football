using System.Text.Json;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using YahooDataSync.Configuration;
using YahooDataSync.Models;

namespace YahooDataSync.Services;

internal sealed class YahooOAuthStateStore(BlobServiceClient blobServiceClient, YahooStorageOptions storageOptions)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly BlobContainerClient _containerClient = blobServiceClient.GetBlobContainerClient(storageOptions.ContainerName);
    private readonly string _blobName = storageOptions.OAuthStateBlobName;

    internal async Task<YahooOAuthStateSnapshot> GetAsync(CancellationToken cancellationToken)
    {
        await EnsureContainerAsync(cancellationToken);
        var blobClient = _containerClient.GetBlobClient(_blobName);

        try
        {
            var download = await blobClient.DownloadContentAsync(cancellationToken);
            var state = download.Value.Content.ToObjectFromJson<YahooOAuthState>(SerializerOptions) ?? throw new InvalidDataException("The Yahoo OAuth state blob is invalid.");
            return new YahooOAuthStateSnapshot(state, download.Value.Details.ETag);
        }
        catch (RequestFailedException exception) when (exception.Status == StatusCodes.Status404NotFound)
        {
            return new YahooOAuthStateSnapshot(new YahooOAuthState(), null);
        }
    }

    internal async Task<YahooOAuthStateSnapshot> SaveAsync(YahooOAuthState state, ETag? expectedETag, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state);
        await EnsureContainerAsync(cancellationToken);

        var conditions = expectedETag.HasValue
            ? new BlobRequestConditions { IfMatch = expectedETag.Value }
            : new BlobRequestConditions { IfNoneMatch = ETag.All };
        var options = new BlobUploadOptions
        {
            Conditions = conditions,
            HttpHeaders = new BlobHttpHeaders { ContentType = "application/json" }
        };

        try
        {
            var response = await _containerClient.GetBlobClient(_blobName).UploadAsync(BinaryData.FromObjectAsJson(state, SerializerOptions), options, cancellationToken);
            return new YahooOAuthStateSnapshot(state, response.Value.ETag);
        }
        catch (RequestFailedException exception) when (exception.Status is StatusCodes.Status409Conflict or StatusCodes.Status412PreconditionFailed)
        {
            throw new YahooAuthStateConcurrencyException("Yahoo OAuth state changed concurrently. Retry the operation against the latest state.", exception);
        }
    }

    private async Task EnsureContainerAsync(CancellationToken cancellationToken)
    {
        await _containerClient.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);
    }
}

internal sealed class YahooAuthStateConcurrencyException(string message, Exception innerException) : InvalidOperationException(message, innerException);
