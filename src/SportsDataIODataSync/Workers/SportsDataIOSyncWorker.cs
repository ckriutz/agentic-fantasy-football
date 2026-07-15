using Azure;
using System.Net.Http.Json;
using SportsDataIODataSync.Models;
using SportsDataIODataSync.Services;

namespace SportsDataIODataSync.Workers;

public sealed class SportsDataIOSyncWorker(SportsDataApiClient sportsDataApiClient, SportsDataSnapshotStorage sportsDataSnapshotStorage, IHttpClientFactory httpClientFactory, ILogger<SportsDataIOSyncWorker> logger) : BackgroundService
{
    private const int ScheduleHour = 6;
    private const int ScheduleMinute = 45;
    private readonly SportsDataApiClient _sportsDataApiClient = sportsDataApiClient;
    private readonly SportsDataSnapshotStorage _sportsDataSnapshotStorage = sportsDataSnapshotStorage;
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly ILogger<SportsDataIOSyncWorker> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await TrySyncAsync(stoppingToken);

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = GetDelayUntilNextRun(DateTimeOffset.UtcNow, timeZone, ScheduleHour, ScheduleMinute);
            _logger.LogInformation("Next SportsDataIO sync is scheduled for {NextRunUtc}.", DateTimeOffset.UtcNow.Add(delay));

            await Task.Delay(delay, stoppingToken);
            await TrySyncAsync(stoppingToken);
        }
    }

    private async Task TrySyncAsync(CancellationToken cancellationToken)
    {
        try
        {
            var playerSnapshot = await _sportsDataApiClient.GetFantasyPlayersSnapshotAsync(cancellationToken);
            var blobName = await _sportsDataSnapshotStorage.SaveAsync(playerSnapshot, cancellationToken);
            await ImportSnapshotAsync(blobName, playerSnapshot, cancellationToken);
            _logger.LogInformation("Saved and imported SportsDataIO player snapshot: {PlayerCount} players at {ContainerName}/{BlobName}.", playerSnapshot.Players.Count, _sportsDataSnapshotStorage.ContainerName, blobName);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (HttpRequestException exception)
        {
            _logger.LogError(exception, "SportsDataIO sync HTTP request failed with status code {StatusCode}.", exception.StatusCode);
        }
        catch (RequestFailedException exception)
        {
            _logger.LogError(exception, "SportsDataIO player snapshot storage failed with Azure status {StatusCode} and error code {ErrorCode}.", exception.Status, exception.ErrorCode);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "SportsDataIO sync failed.");
        }
    }

    private async Task ImportSnapshotAsync(string blobName, SportsDataPlayersSnapshot snapshot, CancellationToken cancellationToken)
    {
        var request = new SportsDataSnapshotImportRequest(_sportsDataSnapshotStorage.ContainerName, blobName, snapshot.RetrievedAtUtc);
        var leagueApiClient = _httpClientFactory.CreateClient("LeagueApi");
        using var response = await leagueApiClient.PostAsJsonAsync("api/sync/sportsdata", request, cancellationToken);
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
