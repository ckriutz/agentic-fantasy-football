using Microsoft.Extensions.Options;
using LeagueAPI.Configuration;
using LeagueAPI.Services;

namespace LeagueAPI.HostedServices;

public sealed class NightlyYahooSyncService(
    YahooPlayerSyncService yahooPlayerSyncService,
    IOptions<YahooSyncOptions> yahooSyncOptions,
    ILogger<NightlyYahooSyncService> logger) : BackgroundService
{
    private readonly YahooPlayerSyncService _yahooPlayerSyncService = yahooPlayerSyncService;
    private readonly YahooSyncOptions _yahooSyncOptions = yahooSyncOptions.Value;
    private readonly ILogger<NightlyYahooSyncService> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_yahooSyncOptions.Enabled)
        {
            _logger.LogInformation("Yahoo nightly sync is disabled.");
            return;
        }

        if (_yahooSyncOptions.RunOnStartup)
        {
            await TrySyncAsync(stoppingToken);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = GetDelayUntilNextRunUtc(DateTimeOffset.UtcNow, _yahooSyncOptions.DailySyncHourUtc);
            await Task.Delay(delay, stoppingToken);
            await TrySyncAsync(stoppingToken);
        }
    }

    private async Task TrySyncAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation(
                "Starting nightly Yahoo sync for game key {GameKey}, season {Season}, week {Week}.",
                _yahooSyncOptions.DefaultGameKey,
                _yahooSyncOptions.DefaultSeason,
                _yahooSyncOptions.DefaultWeek);

            await _yahooPlayerSyncService.SyncWeeklyStatsAsync(
                _yahooSyncOptions.DefaultGameKey,
                _yahooSyncOptions.DefaultSeason,
                _yahooSyncOptions.DefaultWeek,
                force: false,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Yahoo weekly sync failed.");
        }
    }

    private static TimeSpan GetDelayUntilNextRunUtc(DateTimeOffset nowUtc, int dailySyncHourUtc)
    {
        if (dailySyncHourUtc is < 0 or > 23)
        {
            throw new InvalidOperationException("YahooSync:DailySyncHourUtc must be between 0 and 23.");
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
