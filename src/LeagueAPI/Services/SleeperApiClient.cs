using Microsoft.Extensions.Options;
using LeagueAPI.Configuration;

namespace LeagueAPI.Services;

public sealed class SleeperApiClient(
    IHttpClientFactory httpClientFactory,
    IOptions<SleeperSyncOptions> sleeperSyncOptions)
{
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly SleeperSyncOptions _sleeperSyncOptions = sleeperSyncOptions.Value;

    public async Task<string> GetPlayersJsonAsync(CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient("SleeperApi");

        using var response = await httpClient.GetAsync(_sleeperSyncOptions.PlayersEndpoint, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync(cancellationToken);
    }
}
