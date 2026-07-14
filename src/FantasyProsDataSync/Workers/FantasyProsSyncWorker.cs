using FantasyProsDataSync.Configuration;
using FantasyProsDataSync.Services;
using Microsoft.Extensions.Options;

namespace FantasyProsDataSync.Workers;

public sealed class FantasyProsSyncWorker(FantasyProsApiClient fantasyProsApiClient, FantasyProsSnapshotStorage fantasyProsSnapshotStorage, IOptions<FantasyProsSyncOptions> fantasyProsSyncOptions, ILogger<FantasyProsSyncWorker> logger) : BackgroundService
{
    private readonly FantasyProsApiClient _fantasyProsApiClient = fantasyProsApiClient;
    private readonly FantasyProsSnapshotStorage _fantasyProsSnapshotStorage = fantasyProsSnapshotStorage;
    private readonly FantasyProsSyncOptions _fantasyProsSyncOptions = fantasyProsSyncOptions.Value;
    private readonly ILogger<FantasyProsSyncWorker> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await TryGetRankingsAsync(stoppingToken);

        var timeZone = GetTimeZone(_fantasyProsSyncOptions.TimeZoneId);

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = GetDelayUntilNextRun(DateTimeOffset.UtcNow, timeZone, _fantasyProsSyncOptions.ScheduleHour, _fantasyProsSyncOptions.ScheduleMinute);
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
            _logger.LogInformation(
                "Saved FantasyPros master player file for season {Season}, week {Week}: {PlayerCount} players at {BlobName}.",
                playerSnapshot.Season,
                playerSnapshot.Week,
                playerSnapshot.Players.Count,
                blobName);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (HttpRequestException exception)
        {
            _logger.LogError(exception, "FantasyPros rankings request failed with status code {StatusCode}.", exception.StatusCode);
        }
    }

    private static TimeZoneInfo GetTimeZone(string timeZoneId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException exception)
        {
            throw new InvalidOperationException($"FantasyProsSync:TimeZoneId '{timeZoneId}' was not found.", exception);
        }
        catch (InvalidTimeZoneException exception)
        {
            throw new InvalidOperationException($"FantasyProsSync:TimeZoneId '{timeZoneId}' is invalid.", exception);
        }
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
