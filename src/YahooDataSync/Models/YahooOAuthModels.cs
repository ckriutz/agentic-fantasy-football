using System.Text.Json.Serialization;

namespace YahooDataSync.Models;

internal sealed record YahooAuthorizationExchangeRequest(string? Code, string? State, string? RedirectUrl);

internal sealed record YahooAuthorizationUrlResponse(string AuthorizationUrl, string RedirectUri, string State);

internal sealed record YahooAuthStatus(bool IsConfigured, bool HasAccessToken, bool HasRefreshToken, DateTimeOffset? AccessTokenExpiresAtUtc, DateTimeOffset? LastRefreshedAtUtc, bool HasPendingAuthorizationState);

internal sealed record YahooOAuthState(string? RefreshToken = null, string? TokenType = null, string? Scope = null, DateTimeOffset? IssuedAtUtc = null, DateTimeOffset? LastRefreshedAtUtc = null, string? AuthorizationState = null, DateTimeOffset? AuthorizationStateExpiresAtUtc = null);

internal sealed record YahooOAuthStateSnapshot(YahooOAuthState State, Azure.ETag? ETag);

internal sealed class YahooTokenResponse
{
    [JsonPropertyName("access_token")]
    public required string AccessToken { get; init; }

    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; init; }

    [JsonPropertyName("token_type")]
    public string? TokenType { get; init; }

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; init; }

    [JsonPropertyName("scope")]
    public string? Scope { get; init; }
}
