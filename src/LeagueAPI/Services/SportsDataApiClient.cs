using LeagueAPI.Configuration;
using Microsoft.Extensions.Options;

namespace LeagueAPI.Services;

public sealed class SportsDataApiClient(
    IHttpClientFactory httpClientFactory,
    IOptions<SportsDataSyncOptions> sportsDataSyncOptions)
{
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly SportsDataSyncOptions _sportsDataSyncOptions = sportsDataSyncOptions.Value;

    public async Task<string> GetFantasyPlayersJsonAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_sportsDataSyncOptions.ApiKey))
        {
            throw new InvalidOperationException("SportsDataSync:ApiKey must be configured.");
        }

        var httpClient = _httpClientFactory.CreateClient("SportsDataApi");
        var requestUri =
            $"{_sportsDataSyncOptions.BaseUrl.TrimEnd('/')}/{_sportsDataSyncOptions.FantasyPlayersEndpoint.TrimStart('/')}?key={Uri.EscapeDataString(_sportsDataSyncOptions.ApiKey)}";

        using var response = await httpClient.GetAsync(requestUri, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync(cancellationToken);
    }
}
