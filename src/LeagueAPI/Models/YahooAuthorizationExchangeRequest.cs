namespace LeagueAPI.Models;

public sealed class YahooAuthorizationExchangeRequest
{
    public string? Code { get; init; }

    public string? RedirectUrl { get; init; }
}
