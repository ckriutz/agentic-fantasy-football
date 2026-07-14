using FantasyProsDataSync.Models;
using System.Net;
using System.Net.Http.Json;

namespace FantasyProsDataSync.Services;

public sealed class FantasyProsApiClient(HttpClient httpClient, IHttpClientFactory httpClientFactory, ILogger<FantasyProsApiClient> logger)
{
    private static readonly TimeSpan DelayBetweenRequests = TimeSpan.FromSeconds(2);

    private readonly HttpClient _httpClient = httpClient;
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly ILogger<FantasyProsApiClient> _logger = logger;

    public static readonly IReadOnlyList<string> Positions = ["QB", "RB", "WR", "TE", "K", "OP", "FLX", "DST"];

    public async Task<FantasyProsPlayersSnapshot> GetConsensusRankingsAsync(CancellationToken cancellationToken)
    {
        var leagueApiClient = _httpClientFactory.CreateClient("LeagueApi");
        var leagueState = await leagueApiClient.GetFromJsonAsync<LeagueState>("api/league/state", cancellationToken) ?? throw new InvalidOperationException("LeagueAPI returned an empty league state response.");

        var players = new List<FantasyProsRankingPlayer>();

        for (var positionIndex = 0; positionIndex < Positions.Count; positionIndex++)
        {
            var position = Positions[positionIndex];

            if (positionIndex > 0)
            {
                await Task.Delay(DelayBetweenRequests, cancellationToken);
            }

            var requestUri = $"NFL/{leagueState.Season}/consensus-rankings?position={Uri.EscapeDataString(position)}&scoring=PPR";
            _logger.LogInformation("Requesting FantasyPros consensus rankings for position {Position} at {RequestUri}.", position, requestUri);
            var rankings = await GetRankingsAsync(requestUri, position, cancellationToken);

            players.AddRange(rankings.Players);
        }

        var uniquePlayers = players.DistinctBy(player => player.PlayerId).ToArray();

        return new FantasyProsPlayersSnapshot(leagueState.Season, leagueState.Week, DateTimeOffset.UtcNow, uniquePlayers);
    }

    private async Task<FantasyProsRankingsResponse> GetRankingsAsync(string requestUri, string position, CancellationToken cancellationToken)
    {
        var retryDelays = new[] { TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(30) };
        for (var attempt = 0; attempt <= retryDelays.Length; attempt++)
        {
            try
            {
                using var response = await _httpClient.GetAsync(requestUri, cancellationToken);
                response.EnsureSuccessStatusCode();

                return await response.Content.ReadFromJsonAsync<FantasyProsRankingsResponse>(cancellationToken: cancellationToken) ?? throw new InvalidOperationException($"FantasyPros returned an empty rankings response for position '{position}'.");
            }
            catch (HttpRequestException exception) when (exception.StatusCode == HttpStatusCode.TooManyRequests)
            {
                if (attempt == retryDelays.Length)
                {
                    _logger.LogError(exception, "FantasyPros returned HTTP 429 for position {Position} after {AttemptCount} attempts.", position, attempt + 1);
                    throw;
                }

                var retryDelay = retryDelays[attempt];
                _logger.LogWarning(exception, "FantasyPros returned HTTP 429 for position {Position} on attempt {Attempt}. Retrying in {RetryDelaySeconds} seconds.", position, attempt + 1, retryDelay.TotalSeconds);
                await Task.Delay(retryDelay, cancellationToken);
            }
        }

        throw new InvalidOperationException($"FantasyPros rankings request failed for position '{position}'.");
    }
}
