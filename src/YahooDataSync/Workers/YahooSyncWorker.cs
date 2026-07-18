using System.Text.Json;
using Azure;
using YahooDataSync.Configuration;
using YahooDataSync.Services;

namespace YahooDataSync.Workers;

internal sealed class YahooSyncWorker(YahooSyncOrchestrator syncOrchestrator, YahooSyncOptions options, ILogger<YahooSyncWorker> logger) : BackgroundService
{
    private readonly YahooSyncOrchestrator _syncOrchestrator = syncOrchestrator;
    private readonly YahooSyncOptions _options = options;
    private readonly ILogger<YahooSyncWorker> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Scheduled Yahoo sync is disabled.");
            return;
        }

        if (_options.RunOnStartup)
        {
            await TrySyncAsync(stoppingToken);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = GetDelayUntilNextRunUtc(DateTimeOffset.UtcNow, _options.DailySyncHourUtc, _options.DailySyncMinuteUtc);
            _logger.LogInformation("Next Yahoo sync is scheduled for {NextRunUtc}.", DateTimeOffset.UtcNow.Add(delay));
            await Task.Delay(delay, stoppingToken);
            await TrySyncAsync(stoppingToken);
        }
    }

    private async Task TrySyncAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _syncOrchestrator.RunAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (OperationCanceledException exception)
        {
            _logger.LogError(exception, "Yahoo extraction timed out.");
        }
        catch (Exception exception) when (exception is HttpRequestException or RequestFailedException or JsonException or InvalidDataException or InvalidOperationException)
        {
            _logger.LogError(exception, "Yahoo extraction failed.");
        }
    }

    private static TimeSpan GetDelayUntilNextRunUtc(DateTimeOffset nowUtc, int hourUtc, int minuteUtc)
    {
        var nextRunUtc = new DateTimeOffset(nowUtc.Year, nowUtc.Month, nowUtc.Day, hourUtc, minuteUtc, 0, TimeSpan.Zero);
        if (nextRunUtc <= nowUtc)
        {
            nextRunUtc = nextRunUtc.AddDays(1);
        }

        return nextRunUtc - nowUtc;
    }
}
