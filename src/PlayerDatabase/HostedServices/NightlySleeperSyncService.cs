using Microsoft.Extensions.Options;
using PlayerDatabase.Configuration;
using PlayerDatabase.Services;

namespace PlayerDatabase.HostedServices;

public sealed class NightlySleeperSyncService(
    SleeperPlayerSyncService sleeperSyncService,
    IOptions<SleeperSyncOptions> sleeperSyncOptions,
    ILogger<NightlySleeperSyncService> logger) : BackgroundService
{
    private readonly SleeperPlayerSyncService _sleeperSyncService = sleeperSyncService;
    private readonly SleeperSyncOptions _sleeperSyncOptions = sleeperSyncOptions.Value;
    private readonly ILogger<NightlySleeperSyncService> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_sleeperSyncOptions.Enabled)
        {
            _logger.LogInformation("Sleeper nightly sync is disabled.");
            return;
        }

        if (_sleeperSyncOptions.RunOnStartup)
        {
            await TrySyncAsync(stoppingToken);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = GetDelayUntilNextRunUtc(DateTimeOffset.UtcNow, _sleeperSyncOptions.DailySyncHourUtc);
            await Task.Delay(delay, stoppingToken);
            await TrySyncAsync(stoppingToken);
        }
    }

    private async Task TrySyncAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _sleeperSyncService.SyncPlayersAsync(force: false, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Sleeper player sync failed.");
        }
    }

    private static TimeSpan GetDelayUntilNextRunUtc(DateTimeOffset nowUtc, int dailySyncHourUtc)
    {
        if (dailySyncHourUtc is < 0 or > 23)
        {
            throw new InvalidOperationException("SleeperSync:DailySyncHourUtc must be between 0 and 23.");
        }

        var nextRunUtc = new DateTimeOffset(
            nowUtc.Year,
            nowUtc.Month,
            nowUtc.Day,
            dailySyncHourUtc,
            0,
            0,
            TimeSpan.Zero);

        if (nextRunUtc <= nowUtc)
        {
            nextRunUtc = nextRunUtc.AddDays(1);
        }

        return nextRunUtc - nowUtc;
    }
}
