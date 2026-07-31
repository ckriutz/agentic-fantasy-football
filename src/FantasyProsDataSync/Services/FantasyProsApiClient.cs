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

    public async Task<FantasyProsPointsSnapshot> GetPlayerPointsSnapshotAsync(int season, int? endWeek, CancellationToken cancellationToken)
    {
        var requestUri = $"nfl/{season}/player-points?scoring=PPR";
        if (endWeek is int week)
        {
            requestUri += $"&end={week}";
        }

        _logger.LogInformation("Requesting FantasyPros player points for season {Season} at {RequestUri}.", season, requestUri);
        var response = await GetJsonWithRetryAsync<FantasyProsPointsResponse>(requestUri, $"player-points season {season}", cancellationToken);
        var servedSeason = response.Season ?? throw new InvalidOperationException("FantasyPros returned player points without a season.");
        var servedScoring = response.Scoring ?? throw new InvalidOperationException("FantasyPros returned player points without a scoring format.");

        return new FantasyProsPointsSnapshot(servedSeason, servedScoring, DateTimeOffset.UtcNow, response.Players);
    }

    private async Task<FantasyProsRankingsResponse> GetRankingsAsync(string requestUri, string position, CancellationToken cancellationToken)
    {
        return await GetJsonWithRetryAsync<FantasyProsRankingsResponse>(requestUri, $"position '{position}'", cancellationToken);
    }

    private async Task<T> GetJsonWithRetryAsync<T>(string requestUri, string requestDescription, CancellationToken cancellationToken)
    {
        var retryDelays = new[] { TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(30) };
        for (var attempt = 0; attempt <= retryDelays.Length; attempt++)
        {
            try
            {
                using var response = await _httpClient.GetAsync(requestUri, cancellationToken);
                response.EnsureSuccessStatusCode();

                return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken) ?? throw new InvalidOperationException($"FantasyPros returned an empty response for {requestDescription}.");
            }
            catch (HttpRequestException exception) when (exception.StatusCode == HttpStatusCode.TooManyRequests)
            {
                if (attempt == retryDelays.Length)
                {
                    _logger.LogError(exception, "FantasyPros returned HTTP 429 for {RequestDescription} after {AttemptCount} attempts.", requestDescription, attempt + 1);
                    throw;
                }

                var retryDelay = retryDelays[attempt];
                _logger.LogWarning(exception, "FantasyPros returned HTTP 429 for {RequestDescription} on attempt {Attempt}. Retrying in {RetryDelaySeconds} seconds.", requestDescription, attempt + 1, retryDelay.TotalSeconds);
                await Task.Delay(retryDelay, cancellationToken);
            }
        }

        throw new InvalidOperationException($"FantasyPros request failed for {requestDescription}.");
    }
}
