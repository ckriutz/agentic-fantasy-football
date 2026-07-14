using FantasyProsDataSync.Configuration;
using FantasyProsDataSync.Models;
using System.Net.Http.Json;

namespace FantasyProsDataSync.Services;

public sealed class FantasyProsApiClient(HttpClient httpClient, IHttpClientFactory httpClientFactory)
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;

    public static readonly IReadOnlyList<string> Positions = ["QB", "RB", "WR", "TE", "K", "OP", "FLX", "DST"];

    public async Task<FantasyProsPlayersSnapshot> GetConsensusRankingsAsync(CancellationToken cancellationToken)
    {
        var leagueApiClient = _httpClientFactory.CreateClient("LeagueApi");
        var leagueState = await leagueApiClient.GetFromJsonAsync<FantasyProsLeagueState>("api/league/state", cancellationToken)
            ?? throw new InvalidOperationException("LeagueAPI returned an empty league state response.");

        var players = new List<FantasyProsRankingPlayer>();

        foreach (var position in Positions)
        {
            var requestUri = $"NFL/{leagueState.Season}/consensus-rankings?position={Uri.EscapeDataString(position)}&scoring=PPR";
            var rankings = await _httpClient.GetFromJsonAsync<FantasyProsRankingsResponse>(requestUri, cancellationToken)
                ?? throw new InvalidOperationException($"FantasyPros returned an empty rankings response for position '{position}'.");

            players.AddRange(rankings.Players);
        }

        var uniquePlayers = players
            .DistinctBy(player => player.PlayerId)
            .ToArray();

        return new FantasyProsPlayersSnapshot(leagueState.Season, leagueState.Week, DateTimeOffset.UtcNow, uniquePlayers);
    }
}
