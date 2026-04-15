namespace LeagueAPI.Models;

public sealed class YahooOAuthState
{
    public string? AccessToken { get; set; }

    public string? RefreshToken { get; set; }

    public string? TokenType { get; set; }

    public int? ExpiresInSeconds { get; set; }

    public DateTimeOffset? IssuedAtUtc { get; set; }

    public DateTimeOffset? AccessTokenExpiresAtUtc { get; set; }

    public DateTimeOffset? LastRefreshedAtUtc { get; set; }

    public string? Scope { get; set; }

    public string? AuthorizationState { get; set; }
}
