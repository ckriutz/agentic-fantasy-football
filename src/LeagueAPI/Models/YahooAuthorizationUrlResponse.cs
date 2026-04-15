namespace LeagueAPI.Models;

public sealed class YahooAuthorizationUrlResponse
{
    public required string AuthorizationUrl { get; init; }

    public required string RedirectUri { get; init; }

    public required string State { get; init; }
}
