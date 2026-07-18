using YahooDataSync.Configuration;
using YahooDataSync.Models;

namespace YahooDataSync.Services;

internal sealed class YahooSyncOrchestrator(YahooFantasyApiClient yahooFantasyApiClient, YahooSnapshotStorage snapshotStorage, LeagueApiClient leagueApiClient, YahooSyncOptions options, ILogger<YahooSyncOrchestrator> logger)
{
    private readonly YahooFantasyApiClient _yahooFantasyApiClient = yahooFantasyApiClient;
    private readonly YahooSnapshotStorage _snapshotStorage = snapshotStorage;
    private readonly LeagueApiClient _leagueApiClient = leagueApiClient;
    private readonly YahooSyncOptions _options = options;
    private readonly ILogger<YahooSyncOrchestrator> _logger = logger;
    private readonly SemaphoreSlim _syncLock = new(1, 1);

    internal async Task<YahooSyncResult> RunAsync(CancellationToken cancellationToken)
    {
        await _syncLock.WaitAsync(cancellationToken);
        try
        {
            var leagueState = await _leagueApiClient.GetLeagueStateAsync(cancellationToken);
            if (leagueState.Season is < 2000 or > 2100)
            {
                throw new InvalidOperationException($"LeagueAPI returned invalid season {leagueState.Season}.");
            }

            if (leagueState.Week is < 1 or > 17)
            {
                throw new InvalidOperationException($"Yahoo weekly extraction requires LeagueAPI week 1 through 17; current week is {leagueState.Week}.");
            }

            var gameKey = string.IsNullOrWhiteSpace(_options.GameKey)
                ? await _yahooFantasyApiClient.GetGameKeyAsync(leagueState.Season, cancellationToken)
                : _options.GameKey.Trim();
            var retrievedAtUtc = DateTimeOffset.UtcNow;
            var pages = await GetAllPagesAsync(gameKey, leagueState.Week, cancellationToken);
            var snapshot = new YahooPlayersSnapshot(gameKey, leagueState.Season, leagueState.Week, retrievedAtUtc, pages);
            var blobName = await _snapshotStorage.SaveAsync(snapshot, cancellationToken);
            var importRequest = new YahooSnapshotImportRequest(_snapshotStorage.ContainerName, blobName, gameKey, leagueState.Season, leagueState.Week, retrievedAtUtc);
            await _leagueApiClient.ImportYahooSnapshotAsync(importRequest, cancellationToken);

            _logger.LogInformation("Yahoo snapshot saved and imported from {ContainerName}/{BlobName} for game {GameKey}, season {Season}, week {Week}, with {PageCount} pages.", _snapshotStorage.ContainerName, blobName, gameKey, leagueState.Season, leagueState.Week, pages.Count);
            return new YahooSyncResult(_snapshotStorage.ContainerName, blobName, gameKey, leagueState.Season, leagueState.Week, retrievedAtUtc, pages.Count);
        }
        finally
        {
            _syncLock.Release();
        }
    }

    private async Task<List<System.Text.Json.JsonElement>> GetAllPagesAsync(string gameKey, int week, CancellationToken cancellationToken)
    {
        var pages = new List<System.Text.Json.JsonElement>();
        var start = 0;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var page = await _yahooFantasyApiClient.GetWeeklyPlayerStatsAsync(gameKey, week, start, _options.PageSize, cancellationToken);
            var playerCount = YahooFantasyApiClient.CountPlayers(page);
            if (playerCount == 0)
            {
                break;
            }

            pages.Add(page);
            if (playerCount < _options.PageSize)
            {
                break;
            }

            start += _options.PageSize;
        }

        if (pages.Count == 0)
        {
            throw new InvalidDataException($"Yahoo returned no player statistics for game {gameKey}, week {week}.");
        }

        return pages;
    }
}
