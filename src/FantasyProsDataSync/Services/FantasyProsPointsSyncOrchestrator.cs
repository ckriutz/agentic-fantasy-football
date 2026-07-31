using System.Net.Http.Json;
using FantasyProsDataSync.Models;

namespace FantasyProsDataSync.Services;

public sealed class FantasyProsPointsSyncOrchestrator(FantasyProsApiClient fantasyProsApiClient, FantasyProsSnapshotStorage snapshotStorage, IHttpClientFactory httpClientFactory, ILogger<FantasyProsPointsSyncOrchestrator> logger)
{
    private readonly FantasyProsApiClient _fantasyProsApiClient = fantasyProsApiClient;
    private readonly FantasyProsSnapshotStorage _snapshotStorage = snapshotStorage;
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly ILogger<FantasyProsPointsSyncOrchestrator> _logger = logger;
    private readonly SemaphoreSlim _syncLock = new(1, 1);

    public async Task<FantasyProsPointsSyncResult> RunAsync(int? endWeek, CancellationToken cancellationToken)
    {
        await _syncLock.WaitAsync(cancellationToken);
        try
        {
            var leagueApiClient = _httpClientFactory.CreateClient("LeagueApi");
            var leagueState = await leagueApiClient.GetFromJsonAsync<LeagueState>("api/league/state", cancellationToken) ?? throw new InvalidOperationException("LeagueAPI returned an empty league state response.");
            if (leagueState.Season is < 2000 or > 2100)
            {
                throw new InvalidOperationException($"LeagueAPI returned invalid season {leagueState.Season}.");
            }

            var resolvedEndWeek = endWeek ?? leagueState.Week;
            var snapshot = await _fantasyProsApiClient.GetPlayerPointsSnapshotAsync(leagueState.Season, resolvedEndWeek, cancellationToken);
            var blobName = await _snapshotStorage.SavePointsAsync(snapshot, cancellationToken);
            var importRequest = new FantasyProsPointsImportRequest(_snapshotStorage.ContainerName, blobName, leagueState.Season, snapshot.Season, snapshot.Scoring, resolvedEndWeek, snapshot.RetrievedAtUtc);

            using var response = await leagueApiClient.PostAsJsonAsync("api/sync/fantasypros/points", importRequest, cancellationToken);
            response.EnsureSuccessStatusCode();

            _logger.LogInformation("FantasyPros points snapshot saved and imported from {ContainerName}/{BlobName} for requested season {RequestedSeason}, served season {ServedSeason}, end week {EndWeek}, with {PlayerCount} players.", _snapshotStorage.ContainerName, blobName, leagueState.Season, snapshot.Season, resolvedEndWeek, snapshot.Players.Count);
            return new FantasyProsPointsSyncResult(_snapshotStorage.ContainerName, blobName, leagueState.Season, snapshot.Season, resolvedEndWeek, snapshot.Players.Count, snapshot.RetrievedAtUtc);
        }
        finally
        {
            _syncLock.Release();
        }
    }
}
