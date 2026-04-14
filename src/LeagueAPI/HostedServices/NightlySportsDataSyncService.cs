using LeagueAPI.Configuration;
using LeagueAPI.Services;
using Microsoft.Extensions.Options;

namespace LeagueAPI.HostedServices;

public sealed class NightlySportsDataSyncService(
    SportsDataPlayerSyncService sportsDataPlayerSyncService,
    IOptions<SportsDataSyncOptions> sportsDataSyncOptions,
    ILogger<NightlySportsDataSyncService> logger) : BackgroundService
{
    private readonly SportsDataPlayerSyncService _sportsDataPlayerSyncService = sportsDataPlayerSyncService;
    private readonly SportsDataSyncOptions _sportsDataSyncOptions = sportsDataSyncOptions.Value;
    private readonly ILogger<NightlySportsDataSyncService> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_sportsDataSyncOptions.Enabled)
        {
            _logger.LogInformation("SportsData nightly sync is disabled.");
            return;
        }

        if (_sportsDataSyncOptions.RunOnStartup)
        {
            await TrySyncAsync(stoppingToken);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = GetDelayUntilNextRunUtc(DateTimeOffset.UtcNow, _sportsDataSyncOptions.DailySyncHourUtc);
            await Task.Delay(delay, stoppingToken);
            await TrySyncAsync(stoppingToken);
        }
    }

    private async Task TrySyncAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _sportsDataPlayerSyncService.SyncPlayersAsync(force: false, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "SportsData player sync failed.");
        }
    }

    private static TimeSpan GetDelayUntilNextRunUtc(DateTimeOffset nowUtc, int dailySyncHourUtc)
    {
        if (dailySyncHourUtc is < 0 or > 23)
        {
            throw new InvalidOperationException("SportsDataSync:DailySyncHourUtc must be between 0 and 23.");
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
