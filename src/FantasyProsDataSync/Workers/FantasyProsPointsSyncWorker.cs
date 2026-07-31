using Azure;
using FantasyProsDataSync.Services;

namespace FantasyProsDataSync.Workers;

public sealed class FantasyProsPointsSyncWorker(FantasyProsPointsSyncOrchestrator orchestrator, IConfiguration configuration, ILogger<FantasyProsPointsSyncWorker> logger) : BackgroundService
{
    private const int DefaultIntervalMinutes = 240;
    private readonly FantasyProsPointsSyncOrchestrator _orchestrator = orchestrator;
    private readonly IConfiguration _configuration = configuration;
    private readonly ILogger<FantasyProsPointsSyncWorker> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!IsEnabled())
        {
            _logger.LogInformation("FantasyPros points sync worker is disabled via FANTASYPROS_POINTS_SYNC_ENABLED.");
            return;
        }

        var interval = GetInterval();
        _logger.LogInformation("FantasyPros points sync worker started with interval {IntervalMinutes} minutes.", interval.TotalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }

            await TrySyncPointsAsync(stoppingToken);
        }
    }

    private async Task TrySyncPointsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await _orchestrator.RunAsync(null, cancellationToken);
            _logger.LogInformation("FantasyPros points sync completed for requested season {RequestedSeason}, served season {ServedSeason}, end week {EndWeek}: {PlayerCount} players at {BlobName}.", result.RequestedSeason, result.ServedSeason, result.EndWeek, result.PlayerCount, result.BlobName);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (HttpRequestException exception)
        {
            _logger.LogError(exception, "FantasyPros points sync HTTP request failed with status code {StatusCode}.", exception.StatusCode);
        }
        catch (RequestFailedException exception)
        {
            _logger.LogError(exception, "FantasyPros points snapshot storage failed with Azure status {StatusCode} and error code {ErrorCode}.", exception.Status, exception.ErrorCode);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "FantasyPros points sync failed.");
        }
    }

    private bool IsEnabled()
    {
        var raw = _configuration["FANTASYPROS_POINTS_SYNC_ENABLED"];
        return string.IsNullOrWhiteSpace(raw) || !bool.TryParse(raw, out var enabled) || enabled;
    }

    private TimeSpan GetInterval()
    {
        var raw = _configuration["FANTASYPROS_POINTS_SYNC_INTERVAL_MINUTES"];
        if (int.TryParse(raw, out var minutes) && minutes > 0)
        {
            return TimeSpan.FromMinutes(minutes);
        }

        return TimeSpan.FromMinutes(DefaultIntervalMinutes);
    }
}
