using Azure;
using System.Net.Http.Json;
using FantasyProsDataSync.Models;
using FantasyProsDataSync.Services;

namespace FantasyProsDataSync.Workers;

public sealed class FantasyProsSyncWorker(FantasyProsApiClient fantasyProsApiClient, FantasyProsSnapshotStorage fantasyProsSnapshotStorage, IHttpClientFactory httpClientFactory, ILogger<FantasyProsSyncWorker> logger) : BackgroundService
{
    private const int ScheduleHour = 6;
    private const int ScheduleMinute = 30;
    private readonly FantasyProsApiClient _fantasyProsApiClient = fantasyProsApiClient;
    private readonly FantasyProsSnapshotStorage _fantasyProsSnapshotStorage = fantasyProsSnapshotStorage;
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly ILogger<FantasyProsSyncWorker> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await TryGetRankingsAsync(stoppingToken);

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = GetDelayUntilNextRun(DateTimeOffset.UtcNow, timeZone, ScheduleHour, ScheduleMinute);
            _logger.LogInformation("Next FantasyPros sync is scheduled for {NextRunUtc}.", DateTimeOffset.UtcNow.Add(delay));

            await Task.Delay(delay, stoppingToken);
            await TryGetRankingsAsync(stoppingToken);
        }
    }

    private async Task TryGetRankingsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var playerSnapshot = await _fantasyProsApiClient.GetConsensusRankingsAsync(cancellationToken);
            var blobName = await _fantasyProsSnapshotStorage.SaveAsync(playerSnapshot, cancellationToken);
            await ImportSnapshotAsync(blobName, playerSnapshot, cancellationToken);
            _logger.LogInformation("Saved and imported FantasyPros player file for season {Season}, week {Week}: {PlayerCount} players at {BlobName}.", playerSnapshot.Season, playerSnapshot.Week, playerSnapshot.Players.Count, blobName);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (HttpRequestException exception)
        {
            _logger.LogError(exception, "FantasyPros sync HTTP request failed with status code {StatusCode}.", exception.StatusCode);
        }
        catch (RequestFailedException exception)
        {
            _logger.LogError(exception, "FantasyPros player snapshot storage failed with Azure status {StatusCode} and error code {ErrorCode}.", exception.Status, exception.ErrorCode);
        }
    }

    private async Task ImportSnapshotAsync(string blobName, FantasyProsPlayersSnapshot snapshot, CancellationToken cancellationToken)
    {
        var request = new FantasyProsSnapshotImportRequest(_fantasyProsSnapshotStorage.ContainerName, blobName, snapshot.Season, snapshot.Week, snapshot.RetrievedAtUtc);

        var leagueApiClient = _httpClientFactory.CreateClient("LeagueApi");
        using var response = await leagueApiClient.PostAsJsonAsync("api/sync/fantasypros", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private static TimeSpan GetDelayUntilNextRun(DateTimeOffset nowUtc, TimeZoneInfo timeZone, int scheduleHour, int scheduleMinute)
    {
        var localNow = TimeZoneInfo.ConvertTime(nowUtc, timeZone);
        var nextRunLocal = new DateTime(localNow.Year, localNow.Month, localNow.Day, scheduleHour, scheduleMinute, 0, DateTimeKind.Unspecified);
        var nextRunUtc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(nextRunLocal, timeZone));

        if (nextRunUtc < nowUtc)
        {
            nextRunUtc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(nextRunLocal.AddDays(1), timeZone));
        }

        return nextRunUtc - nowUtc;
    }
}
