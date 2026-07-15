using Azure;
using System.Net.Http.Json;
using SleeperSync.Models;
using SleeperSync.Services;

namespace SleeperSync.Workers;

public sealed class SleeperSyncWorker(SleeperApiClient sleeperApiClient, SleeperSnapshotStorage sleeperSnapshotStorage, IHttpClientFactory httpClientFactory, ILogger<SleeperSyncWorker> logger) : BackgroundService
{
    private const int ScheduleHour = 6;
    private const int ScheduleMinute = 15;
    private readonly SleeperApiClient _sleeperApiClient = sleeperApiClient;
    private readonly SleeperSnapshotStorage _sleeperSnapshotStorage = sleeperSnapshotStorage;
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly ILogger<SleeperSyncWorker> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await TrySyncAsync(stoppingToken);

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = GetDelayUntilNextRun(DateTimeOffset.UtcNow, timeZone, ScheduleHour, ScheduleMinute);
            _logger.LogInformation("Next Sleeper sync is scheduled for {NextRunUtc}.", DateTimeOffset.UtcNow.Add(delay));

            await Task.Delay(delay, stoppingToken);
            await TrySyncAsync(stoppingToken);
        }
    }

    private async Task TrySyncAsync(CancellationToken cancellationToken)
    {
        try
        {
            var todaysBlobName = _sleeperSnapshotStorage.GetBlobNameForUtc(DateTimeOffset.UtcNow);
            if (await _sleeperSnapshotStorage.ExistsAsync(todaysBlobName, cancellationToken))
            {
                _logger.LogInformation("Skipping Sleeper sync; today's snapshot already exists at {ContainerName}/{BlobName}.", _sleeperSnapshotStorage.ContainerName, todaysBlobName);
                return;
            }

            var playerSnapshot = await _sleeperApiClient.GetPlayersSnapshotAsync(cancellationToken);
            var blobName = await _sleeperSnapshotStorage.SaveAsync(playerSnapshot, cancellationToken);
            await ImportSnapshotAsync(blobName, playerSnapshot, cancellationToken);
            _logger.LogInformation("Saved and imported Sleeper player snapshot at {ContainerName}/{BlobName}.", _sleeperSnapshotStorage.ContainerName, blobName);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (HttpRequestException exception)
        {
            _logger.LogError(exception, "Sleeper sync HTTP request failed with status code {StatusCode}.", exception.StatusCode);
        }
        catch (RequestFailedException exception)
        {
            _logger.LogError(exception, "Sleeper player snapshot storage failed with Azure status {StatusCode} and error code {ErrorCode}.", exception.Status, exception.ErrorCode);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Sleeper sync failed.");
        }
    }

    private async Task ImportSnapshotAsync(string blobName, SleeperPlayersSnapshot snapshot, CancellationToken cancellationToken)
    {
        var request = new SleeperSnapshotImportRequest(_sleeperSnapshotStorage.ContainerName, blobName, snapshot.RetrievedAtUtc);
        var leagueApiClient = _httpClientFactory.CreateClient("LeagueApi");
        using var response = await leagueApiClient.PostAsJsonAsync("api/sync/sleeper", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private static TimeSpan GetDelayUntilNextRun(DateTimeOffset nowUtc, TimeZoneInfo timeZone, int scheduleHour, int scheduleMinute)
    {
        var localNow = TimeZoneInfo.ConvertTime(nowUtc, timeZone);
        var nextRunLocal = new DateTime(localNow.Year, localNow.Month, localNow.Day, scheduleHour, scheduleMinute, 0, DateTimeKind.Unspecified);
        var nextRunUtc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(nextRunLocal, timeZone));

        if (nextRunUtc <= nowUtc)
        {
            nextRunUtc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(nextRunLocal.AddDays(1), timeZone));
        }

        return nextRunUtc - nowUtc;
    }
}
