namespace LeagueAPI.Models;

public sealed class YahooAuthStatus
{
    public bool IsConfigured { get; set; }

    public bool HasAccessToken { get; set; }

    public bool HasRefreshToken { get; set; }

    public DateTimeOffset? AccessTokenExpiresAtUtc { get; set; }

    public DateTimeOffset? LastRefreshedAtUtc { get; set; }

    public bool HasPendingAuthorizationState { get; set; }
}
