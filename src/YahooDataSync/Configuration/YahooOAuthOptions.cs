namespace YahooDataSync.Configuration;

internal sealed class YahooOAuthOptions
{
    public required string ClientId { get; init; }

    public required string ClientSecret { get; init; }

    public required string RedirectUri { get; init; }

    public required string AuthorizationUrl { get; init; }

    public required string TokenUrl { get; init; }

    public required string FantasyApiBaseUrl { get; init; }

    public required string Scope { get; init; }
}
