namespace YahooDataSync.Configuration;

internal sealed class YahooStorageOptions
{
    public required string ContainerName { get; init; }

    public required string OAuthStateBlobName { get; init; }
}
