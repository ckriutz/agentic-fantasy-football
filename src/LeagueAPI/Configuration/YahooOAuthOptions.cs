namespace LeagueAPI.Configuration;

public sealed class YahooOAuthOptions
{
    public const string SectionName = "YahooOAuth";

    public string ClientId { get; init; } = string.Empty;

    public string ClientSecret { get; init; } = string.Empty;

    public string RedirectUri { get; init; } = "https://localhost:3000";

    public string AuthorizationUrl { get; init; } = "https://api.login.yahoo.com/oauth2/request_auth";

    public string TokenUrl { get; init; } = "https://api.login.yahoo.com/oauth2/get_token";

    public string FantasyApiBaseUrl { get; init; } = "https://fantasysports.yahooapis.com/fantasy/v2";

    public string Scope { get; init; } = "fspt-r";
}
