using ModelContextProtocol.Server;
using Microsoft.Extensions.Options;
using LeagueAPI.Configuration;
using LeagueAPI.HostedServices;
using LeagueAPI.Models;
using LeagueAPI.Services;
using LeagueAPI.Tools;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<SleeperSyncOptions>(
    builder.Configuration.GetSection(SleeperSyncOptions.SectionName));

builder.Services.AddMemoryCache();

builder.Services.AddHttpClient("SleeperApi");

builder.Services.AddSingleton<JsonFileSyncStateStore>();
builder.Services.AddSingleton<FileSleeperSnapshotStore>();
builder.Services.AddSingleton<SleeperApiClient>();
builder.Services.AddSingleton<SleeperPlayerSyncService>();

var sleeperSyncOptions =
    builder.Configuration
        .GetSection(SleeperSyncOptions.SectionName)
        .Get<SleeperSyncOptions>() ?? new SleeperSyncOptions();

var effectivePlayerCatalogMode = ResolvePlayerCatalogMode(sleeperSyncOptions, builder.Configuration);

switch (effectivePlayerCatalogMode)
{
    case PlayerCatalogStorageMode.SqlServer:
        builder.Services.AddSingleton<SqlServerPlayerCatalogStore>();
        builder.Services.AddSingleton<IPlayerCatalogReader>(serviceProvider =>
            serviceProvider.GetRequiredService<SqlServerPlayerCatalogStore>());
        builder.Services.AddSingleton<IPlayerCatalogPersistence>(serviceProvider =>
            serviceProvider.GetRequiredService<SqlServerPlayerCatalogStore>());
        break;
    default:
        builder.Services.AddSingleton<IPlayerCatalogReader, SnapshotPlayerCatalogReader>();
        break;
}

builder.Services.AddHostedService<NightlySleeperSyncService>();

builder.Services.AddMcpServer()
    .WithHttpTransport(options => options.Stateless = true)
    .WithTools<PlayerCatalogTools>();

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
        "/api/players?name=&team=&position=&limit=",
        "/api/sync/sleeper/latest",
        "/api/sync/sleeper?force=true"
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
    int? limit,
    IPlayerCatalogReader playerCatalogReader,
    CancellationToken cancellationToken) =>
{
    var query = new PlayerQuery
    {
        Name = name,
        Team = team,
        Position = position,
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
            PlayerCatalogStorageMode.SqlServer,
        PlayerCatalogStorageMode.Auto => PlayerCatalogStorageMode.SnapshotOnly,
        _ => options.Mode
    };
}
