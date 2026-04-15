using System.Net;
using System.Net.Http.Headers;
using LeagueAPI.Configuration;
using Microsoft.Extensions.Options;

namespace LeagueAPI.Services;

public sealed class YahooFantasyApiClient(
    IHttpClientFactory httpClientFactory,
    YahooOAuthService yahooOAuthService,
    IOptions<YahooOAuthOptions> yahooOAuthOptions)
{
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly YahooOAuthService _yahooOAuthService = yahooOAuthService;
    private readonly YahooOAuthOptions _yahooOAuthOptions = yahooOAuthOptions.Value;

    public Task<string> GetGameInfoJsonAsync(CancellationToken cancellationToken) =>
        GetAsync("game/nfl", cancellationToken);

    public Task<string> GetLeagueSettingsJsonAsync(
        string leagueKey,
        CancellationToken cancellationToken) =>
        GetAsync($"league/{leagueKey}/settings", cancellationToken);

    public Task<string> GetWeeklyPlayerStatsJsonAsync(
        string gameKey,
        int week,
        int start,
        int count,
        CancellationToken cancellationToken) =>
        GetAsync(
            $"game/{gameKey}/players;start={start};count={count}/stats;type=week;week={week}",
            cancellationToken);

    public async Task<string> GetAsync(string relativePath, CancellationToken cancellationToken)
    {
        var accessToken = await _yahooOAuthService.GetValidAccessTokenAsync(cancellationToken);
        using var response = await SendGetAsync(relativePath, accessToken, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            accessToken = await _yahooOAuthService.ForceRefreshAccessTokenAsync(cancellationToken);
            using var retryResponse = await SendGetAsync(relativePath, accessToken, cancellationToken);
            return await ReadSuccessfulResponseAsync(retryResponse, cancellationToken);
        }

        return await ReadSuccessfulResponseAsync(response, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendGetAsync(
        string relativePath,
        string accessToken,
        CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient("YahooFantasyApi");
        var requestUri =
            $"{_yahooOAuthOptions.FantasyApiBaseUrl.TrimEnd('/')}/{EnsureJsonFormat(relativePath.TrimStart('/'))}";

        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        return await httpClient.SendAsync(request, cancellationToken);
    }

    private static string EnsureJsonFormat(string relativePath)
    {
        if (relativePath.Contains("format=json", StringComparison.OrdinalIgnoreCase))
        {
            return relativePath;
        }

        return relativePath.Contains('?')
            ? $"{relativePath}&format=json"
            : $"{relativePath}?format=json";
    }

    private static async Task<string> ReadSuccessfulResponseAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Yahoo API request failed with status {(int)response.StatusCode}: {payload}");
        }

        return payload;
    }
}
