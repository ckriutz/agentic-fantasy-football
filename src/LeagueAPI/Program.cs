using ModelContextProtocol.Server;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using LeagueAPI.Configuration;
using LeagueAPI.Data;
using LeagueAPI.HostedServices;
using LeagueAPI.Models;
using LeagueAPI.Services;
using LeagueAPI.Tools;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<SleeperSyncOptions>(
    builder.Configuration.GetSection(SleeperSyncOptions.SectionName));

builder.Services.Configure<SportsDataSyncOptions>(
    builder.Configuration.GetSection(SportsDataSyncOptions.SectionName));

builder.Services.Configure<YahooOAuthOptions>(
    builder.Configuration.GetSection(YahooOAuthOptions.SectionName));

builder.Services.Configure<YahooSyncOptions>(
    builder.Configuration.GetSection(YahooSyncOptions.SectionName));

builder.Services.AddMemoryCache();

builder.Services.AddHttpClient("SleeperApi");
builder.Services.AddHttpClient("SportsDataApi");
builder.Services.AddHttpClient("YahooOAuth");
builder.Services.AddHttpClient("YahooFantasyApi");
builder.Services.AddDbContextFactory<LeagueApiDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("LeagueAPI");

    if (!string.IsNullOrWhiteSpace(connectionString))
    {
        options.UseNpgsql(connectionString);
    }
});

builder.Services.AddSingleton<JsonFileSyncStateStore>();
builder.Services.AddSingleton<JsonFileYahooAuthStateStore>();
builder.Services.AddSingleton<FileSleeperSnapshotStore>();
builder.Services.AddSingleton<SleeperApiClient>();
builder.Services.AddSingleton<SleeperPlayerSyncService>();
builder.Services.AddSingleton<SportsDataApiClient>();
builder.Services.AddSingleton<SportsDataPlayerSyncService>();
builder.Services.AddSingleton<YahooOAuthService>();
builder.Services.AddSingleton<YahooFantasyApiClient>();
builder.Services.AddSingleton<ScoringService>();
builder.Services.AddSingleton<YahooPlayerSyncService>();
builder.Services.AddSingleton<YahooReadService>();

var sleeperSyncOptions =
    builder.Configuration
        .GetSection(SleeperSyncOptions.SectionName)
        .Get<SleeperSyncOptions>() ?? new SleeperSyncOptions();

var effectivePlayerCatalogMode = ResolvePlayerCatalogMode(sleeperSyncOptions, builder.Configuration);

switch (effectivePlayerCatalogMode)
{
    case PlayerCatalogStorageMode.Postgres:
        builder.Services.AddSingleton<PostgresPlayerCatalogStore>();
        builder.Services.AddSingleton<IPlayerCatalogReader>(serviceProvider =>
            serviceProvider.GetRequiredService<PostgresPlayerCatalogStore>());
        builder.Services.AddSingleton<IPlayerCatalogPersistence>(serviceProvider =>
            serviceProvider.GetRequiredService<PostgresPlayerCatalogStore>());
        break;
    default:
        builder.Services.AddSingleton<IPlayerCatalogReader, SnapshotPlayerCatalogReader>();
        break;
}

builder.Services.AddHostedService<NightlySleeperSyncService>();
builder.Services.AddHostedService<NightlySportsDataSyncService>();

builder.Services.AddMcpServer()
    .WithHttpTransport(options => options.Stateless = true)
    .WithTools<PlayerCatalogTools>()
    .WithTools<YahooReadTools>();

var app = builder.Build();

app.MapGet("/", (IOptions<SleeperSyncOptions> options) => Results.Ok(new
{
    service = "LeagueAPI",
    configuredStorageMode = options.Value.Mode.ToString(),
    effectiveStorageMode = ResolvePlayerCatalogMode(options.Value, app.Configuration).ToString(),
    endpoints = new[]
    {
        "/mcp",
        "/api/players/{sleeperPlayerId}",
        "/api/players/by-yahoo/{yahooId}",
        "/api/players?name=&team=&position=&byeWeek=&minProjectedPoints=&maxAverageDraftPosition=&sortBy=&sortDescending=&limit=",
        "/api/sync/sleeper/latest",
        "/api/sync/sleeper?force=true",
        "/api/sync/sportsdata/latest",
        "/api/sync/yahoo/latest",
        "/api/sync/yahoo/weekly?week=&season=&gameKey=&force=",
        "/api/yahoo/stats/{season}/{week}?position=&limit=",
        "/api/yahoo/stats/player/{sleeperPlayerId}/{season}/week/{week}",
        "/api/yahoo/stats/by-yahoo/{yahooId}/{season}/week/{week}",
        "/api/yahoo/points/{season}/{week}?templateKey=&position=&limit=",
        "/api/yahoo/points/player/{sleeperPlayerId}/{season}/week/{week}?templateKey=",
        "/api/yahoo/points/player/{sleeperPlayerId}/{season}?templateKey=",
        "/api/yahoo/points/by-yahoo/{yahooId}/{season}/week/{week}?templateKey=",
        "/api/yahoo/points/by-yahoo/{yahooId}/{season}?templateKey=",
        "/api/yahoo/scoring-templates?activeOnly=",
        "/api/yahoo/league/{leagueKey}/settings/raw",
        "/api/yahoo/auth/status",
        "/api/yahoo/auth/authorize-url",
        "/api/yahoo/auth/exchange",
        "/api/yahoo/auth/refresh",
        "/api/yahoo/auth/test-connection"
    }
}));

app.MapGet("/api/players/{sleeperPlayerId}", async (
    string sleeperPlayerId,
    IPlayerCatalogReader playerCatalogReader,
    CancellationToken cancellationToken) =>
{
    var player = await playerCatalogReader.GetBySleeperIdAsync(sleeperPlayerId, cancellationToken);
    return player is null ? Results.NotFound() : Results.Ok(player);
});

app.MapGet("/api/players/by-yahoo/{yahooId:int}", async (
    int yahooId,
    IPlayerCatalogReader playerCatalogReader,
    CancellationToken cancellationToken) =>
{
    var player = await playerCatalogReader.GetByYahooIdAsync(yahooId, cancellationToken);
    return player is null ? Results.NotFound() : Results.Ok(player);
});

app.MapGet("/api/players", async (
    string? name,
    string? team,
    string? position,
    int? byeWeek,
    decimal? minProjectedPoints,
    decimal? maxAverageDraftPosition,
    string? sortBy,
    bool? sortDescending,
    int? limit,
    IPlayerCatalogReader playerCatalogReader,
    CancellationToken cancellationToken) =>
{
    var query = new PlayerQuery
    {
        Name = name,
        Team = team,
        Position = position,
        ByeWeek = byeWeek,
        MinProjectedPoints = minProjectedPoints,
        MaxAverageDraftPosition = maxAverageDraftPosition,
        SortBy = sortBy,
        SortDescending = sortDescending ?? false,
        Limit = limit ?? 25
    };

    var players = await playerCatalogReader.QueryAsync(query, cancellationToken);
    return Results.Ok(players);
});

app.MapGet("/api/sync/sleeper/latest", async (
    JsonFileSyncStateStore syncStateStore,
    CancellationToken cancellationToken) =>
{
    var state = await syncStateStore.GetLatestStateAsync(cancellationToken);
    return Results.Ok(state);
});

app.MapPost("/api/sync/sleeper", async (
    bool force,
    SleeperPlayerSyncService sleeperSyncService,
    CancellationToken cancellationToken) =>
{
    var result = await sleeperSyncService.SyncPlayersAsync(force, cancellationToken);
    return Results.Ok(result);
});

app.MapGet("/api/sync/sportsdata/latest", async (
    SportsDataPlayerSyncService sportsDataPlayerSyncService,
    CancellationToken cancellationToken) =>
{
    var state = await sportsDataPlayerSyncService.GetLatestSyncRunAsync(cancellationToken);
    return state is null ? Results.NotFound() : Results.Ok(state);
});

app.MapGet("/api/sync/yahoo/latest", async (
    string? gameKey,
    int? season,
    int? week,
    YahooPlayerSyncService yahooPlayerSyncService,
    CancellationToken cancellationToken) =>
{
    var state = await yahooPlayerSyncService.GetLatestSyncRunAsync(gameKey, season, week, cancellationToken);
    return state is null ? Results.NotFound() : Results.Ok(state);
});

app.MapPost("/api/sync/yahoo/weekly", async (
    int week,
    int? season,
    string? gameKey,
    bool force,
    IOptions<YahooSyncOptions> yahooSyncOptions,
    YahooPlayerSyncService yahooPlayerSyncService,
    CancellationToken cancellationToken) =>
{
    var options = yahooSyncOptions.Value;
    var resolvedGameKey = string.IsNullOrWhiteSpace(gameKey) ? options.DefaultGameKey : gameKey.Trim();
    var resolvedSeason = season ?? options.DefaultSeason;

    var result = await yahooPlayerSyncService.SyncWeeklyStatsAsync(
        resolvedGameKey,
        resolvedSeason,
        week,
        force,
        cancellationToken);

    return Results.Ok(result);
});

app.MapGet("/api/yahoo/stats/{season:int}/{week:int}", async (
    int season,
    int week,
    string? position,
    int? limit,
    YahooReadService yahooReadService,
    CancellationToken cancellationToken) =>
{
    var stats = await yahooReadService.GetWeeklyStatsAsync(
        season,
        week,
        position,
        limit ?? 25,
        cancellationToken);

    return Results.Ok(stats);
});

app.MapGet("/api/yahoo/stats/player/{sleeperPlayerId}/{season:int}/week/{week:int}", async (
    string sleeperPlayerId,
    int season,
    int week,
    YahooReadService yahooReadService,
    CancellationToken cancellationToken) =>
{
    var stat = await yahooReadService.GetPlayerWeeklyStatsBySleeperIdAsync(
        sleeperPlayerId,
        season,
        week,
        cancellationToken);

    return stat is null ? Results.NotFound() : Results.Ok(stat);
});

app.MapGet("/api/yahoo/stats/by-yahoo/{yahooId:int}/{season:int}/week/{week:int}", async (
    int yahooId,
    int season,
    int week,
    YahooReadService yahooReadService,
    CancellationToken cancellationToken) =>
{
    var stat = await yahooReadService.GetPlayerWeeklyStatsByYahooIdAsync(
        yahooId,
        season,
        week,
        cancellationToken);

    return stat is null ? Results.NotFound() : Results.Ok(stat);
});

app.MapGet("/api/yahoo/points/{season:int}/{week:int}", async (
    int season,
    int week,
    string? templateKey,
    string? position,
    int? limit,
    YahooReadService yahooReadService,
    CancellationToken cancellationToken) =>
{
    var points = await yahooReadService.GetWeeklyPointsAsync(
        season,
        week,
        templateKey,
        position,
        limit ?? 25,
        cancellationToken);

    return Results.Ok(points);
});

app.MapGet("/api/yahoo/points/player/{sleeperPlayerId}/{season:int}/week/{week:int}", async (
    string sleeperPlayerId,
    int season,
    int week,
    string? templateKey,
    YahooReadService yahooReadService,
    CancellationToken cancellationToken) =>
{
    var point = await yahooReadService.GetPlayerWeeklyPointsBySleeperIdAsync(
        sleeperPlayerId,
        season,
        week,
        templateKey,
        cancellationToken);

    return point is null ? Results.NotFound() : Results.Ok(point);
});

app.MapGet("/api/yahoo/points/by-yahoo/{yahooId:int}/{season:int}/week/{week:int}", async (
    int yahooId,
    int season,
    int week,
    string? templateKey,
    YahooReadService yahooReadService,
    CancellationToken cancellationToken) =>
{
    var point = await yahooReadService.GetPlayerWeeklyPointsByYahooIdAsync(
        yahooId,
        season,
        week,
        templateKey,
        cancellationToken);

    return point is null ? Results.NotFound() : Results.Ok(point);
});

app.MapGet("/api/yahoo/points/player/{sleeperPlayerId}/{season:int}", async (
    string sleeperPlayerId,
    int season,
    string? templateKey,
    YahooReadService yahooReadService,
    CancellationToken cancellationToken) =>
{
    var seasonPoints = await yahooReadService.GetPlayerSeasonPointsBySleeperIdAsync(
        sleeperPlayerId,
        season,
        templateKey,
        cancellationToken);

    return seasonPoints is null ? Results.NotFound() : Results.Ok(seasonPoints);
});

app.MapGet("/api/yahoo/points/by-yahoo/{yahooId:int}/{season:int}", async (
    int yahooId,
    int season,
    string? templateKey,
    YahooReadService yahooReadService,
    CancellationToken cancellationToken) =>
{
    var seasonPoints = await yahooReadService.GetPlayerSeasonPointsByYahooIdAsync(
        yahooId,
        season,
        templateKey,
        cancellationToken);

    return seasonPoints is null ? Results.NotFound() : Results.Ok(seasonPoints);
});

app.MapGet("/api/yahoo/scoring-templates", async (
    bool? activeOnly,
    YahooReadService yahooReadService,
    CancellationToken cancellationToken) =>
{
    var templates = await yahooReadService.GetScoringTemplatesAsync(
        activeOnly ?? true,
        cancellationToken);

    return Results.Ok(templates);
});

app.MapGet("/api/yahoo/league/{leagueKey}/settings/raw", async (
    string leagueKey,
    YahooFantasyApiClient yahooFantasyApiClient,
    CancellationToken cancellationToken) =>
{
    var payload = await yahooFantasyApiClient.GetLeagueSettingsJsonAsync(leagueKey, cancellationToken);
    return Results.Content(payload, "application/json");
});

app.MapGet("/api/yahoo/auth/status", async (
    YahooOAuthService yahooOAuthService,
    CancellationToken cancellationToken) =>
{
    var status = await yahooOAuthService.GetStatusAsync(cancellationToken);
    return Results.Ok(status);
});

app.MapPost("/api/yahoo/auth/authorize-url", async (
    YahooOAuthService yahooOAuthService,
    CancellationToken cancellationToken) =>
{
    var response = await yahooOAuthService.CreateAuthorizationUrlAsync(cancellationToken);
    return Results.Ok(response);
});

app.MapPost("/api/yahoo/auth/exchange", async (
    YahooAuthorizationExchangeRequest request,
    YahooOAuthService yahooOAuthService,
    CancellationToken cancellationToken) =>
{
    var status = await yahooOAuthService.ExchangeAuthorizationCodeAsync(request, cancellationToken);
    return Results.Ok(status);
});

app.MapPost("/api/yahoo/auth/refresh", async (
    YahooOAuthService yahooOAuthService,
    CancellationToken cancellationToken) =>
{
    var status = await yahooOAuthService.RefreshAccessTokenAsync(cancellationToken);
    return Results.Ok(status);
});

app.MapGet("/api/yahoo/auth/test-connection", async (
    YahooFantasyApiClient yahooFantasyApiClient,
    CancellationToken cancellationToken) =>
{
    var payload = await yahooFantasyApiClient.GetGameInfoJsonAsync(cancellationToken);
    return Results.Content(payload, "application/json");
});

app.MapMcp("/mcp");

app.Run();

static PlayerCatalogStorageMode ResolvePlayerCatalogMode(
    SleeperSyncOptions options,
    IConfiguration configuration)
{
    return options.Mode switch
    {
        PlayerCatalogStorageMode.Auto
            when !string.IsNullOrWhiteSpace(configuration.GetConnectionString("LeagueAPI")) =>
            PlayerCatalogStorageMode.Postgres,
        PlayerCatalogStorageMode.Auto => PlayerCatalogStorageMode.SnapshotOnly,
        _ => options.Mode
    };
}
