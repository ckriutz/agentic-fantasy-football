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

var connectionString = builder.Configuration.GetConnectionString("LeagueAPI");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "ConnectionStrings:LeagueAPI is required. Set it in configuration or via the ConnectionStrings__LeagueAPI environment variable to point at your Postgres database.");
}

builder.Services.AddDbContextFactory<LeagueApiDbContext>(options =>
{
    options.UseNpgsql(connectionString);
});

builder.Services.AddSingleton<PostgresYahooAuthStateStore>();
builder.Services.AddSingleton<SleeperApiClient>();
builder.Services.AddSingleton<SleeperPlayerSyncService>();
builder.Services.AddSingleton<SportsDataApiClient>();
builder.Services.AddSingleton<SportsDataPlayerSyncService>();
builder.Services.AddSingleton<YahooOAuthService>();
builder.Services.AddSingleton<YahooFantasyApiClient>();
builder.Services.AddSingleton<ScoringService>();
builder.Services.AddSingleton<YahooPlayerSyncService>();
builder.Services.AddSingleton<YahooReadService>();

builder.Services.AddSingleton<PostgresRosterStore>();
builder.Services.AddSingleton<IRosterReader>(serviceProvider =>
    serviceProvider.GetRequiredService<PostgresRosterStore>());
builder.Services.AddSingleton<IRosterWriter>(serviceProvider =>
    serviceProvider.GetRequiredService<PostgresRosterStore>());

builder.Services.AddSingleton<PostgresDecisionStore>();
builder.Services.AddSingleton<IDecisionReader>(serviceProvider =>
    serviceProvider.GetRequiredService<PostgresDecisionStore>());
builder.Services.AddSingleton<IDecisionWriter>(serviceProvider =>
    serviceProvider.GetRequiredService<PostgresDecisionStore>());

builder.Services.AddSingleton<PostgresPlayerCatalogStore>();
builder.Services.AddSingleton<IPlayerCatalogReader>(serviceProvider =>
    serviceProvider.GetRequiredService<PostgresPlayerCatalogStore>());
builder.Services.AddSingleton<IPlayerCatalogPersistence>(serviceProvider =>
    serviceProvider.GetRequiredService<PostgresPlayerCatalogStore>());

builder.Services.AddHostedService<NightlySleeperSyncService>();
builder.Services.AddHostedService<NightlySportsDataSyncService>();
builder.Services.AddHostedService<NightlyYahooSyncService>();

builder.Services.AddMcpServer()
    .WithHttpTransport(options => options.Stateless = true)
    .WithTools<PlayerCatalogTools>()
    .WithTools<YahooReadTools>()
    .WithTools<RosterTools>();

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new
{
    service = "LeagueAPI",
    storageMode = "Postgres",
    endpoints = new[]
    {
        "/mcp",
        "/api/players/{sleeperPlayerId}",
        "/api/players/{sleeperPlayerId}/availability",
        "/api/players/by-yahoo/{yahooId}",
        "/api/players?name=&team=&position=&byeWeek=&sortBy=&sortDescending=&limit=",
        "/api/players/roster-status?name=&team=&position=&byeWeek=&sortBy=&sortDescending=&limit=",
        "/api/players/available?name=&team=&position=&byeWeek=&limit=",
        "/api/rosters/{agentId}",
        "/api/rosters/{agentId}/players/{sleeperPlayerId}?acquisitionSource=",
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
        "/api/yahoo/auth/test-connection",
        "/api/decisions (POST: log a decision, GET: list all with ?agentId=&type=&week=&limit=)",
        "/api/decisions/{agentId} (GET: list decisions for agent)"
    }
}));

// --- Decisions ---

app.MapPost("/api/decisions", async (
    LogDecisionRequest request,
    IDecisionWriter decisionWriter,
    CancellationToken cancellationToken) =>
{
    try
    {
        var decision = await decisionWriter.LogDecisionAsync(
            request.AgentId,
            request.Week,
            request.Type,
            request.Reasoning,
            request.Action,
            request.InputTokenCount,
            request.OutputTokenCount,
            request.CachedInputTokenCount,
            request.ReasoningTokenCount,
            cancellationToken);

        return Results.Created($"/api/decisions/{decision.DecisionId}", decision);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapGet("/api/decisions/{agentId}", async (
    string agentId,
    IDecisionReader decisionReader,
    CancellationToken cancellationToken) =>
{
    try
    {
        var decisions = await decisionReader.GetDecisionsByAgentAsync(agentId, cancellationToken);
        return Results.Ok(decisions);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapGet("/api/decisions", async (
    string? agentId,
    string? type,
    int? week,
    int? limit,
    IDecisionReader decisionReader,
    CancellationToken cancellationToken) =>
{
    var decisions = await decisionReader.GetAllDecisionsAsync(
        agentId,
        type,
        week,
        limit ?? 50,
        cancellationToken);

    return Results.Ok(decisions);
});

static PlayerQuery BuildPlayerQuery(
    string? name,
    string? team,
    string? position,
    int? byeWeek,
    string? sortBy,
    bool? sortDescending,
    int? limit)
{
    return new PlayerQuery
    {
        Name = name,
        Team = team,
        Position = position,
        ByeWeek = byeWeek,
        SortBy = sortBy,
        SortDescending = sortDescending ?? false,
        Limit = limit ?? 25
    };
}

static IResult CreateRosterErrorResult(Exception exception)
{
    return exception switch
    {
        ArgumentException argumentException => Results.BadRequest(new { error = argumentException.Message }),
        RosterPlayerNotFoundException notFoundException => Results.NotFound(new { error = notFoundException.Message }),
        RosterConflictException conflictException => Results.Conflict(new { error = conflictException.Message }),
        _ => Results.Problem(exception.Message)
    };
}

app.MapGet("/api/players/roster-status", async (
    string? name,
    string? team,
    string? position,
    int? byeWeek,
    string? sortBy,
    bool? sortDescending,
    int? limit,
    IRosterReader rosterReader,
    CancellationToken cancellationToken) =>
{
    var players = await rosterReader.QueryPlayersAsync(
        BuildPlayerQuery(
            name,
            team,
            position,
            byeWeek,
            sortBy,
            sortDescending,
            limit),
        cancellationToken);

    return Results.Ok(players);
});

app.MapGet("/api/players/available", async (
    string? name,
    string? team,
    string? position,
    int? byeWeek,
    int? limit,
    IRosterReader rosterReader,
    CancellationToken cancellationToken) =>
{
    var players = await rosterReader.GetAvailablePlayersAsync(
        BuildPlayerQuery(
            name,
            team,
            position,
            byeWeek,
            sortBy: null,
            sortDescending: null,
            limit: limit),
        cancellationToken);

    return Results.Ok(players);
});

app.MapGet("/api/rosters/{agentId}", async (
    string agentId,
    IRosterReader rosterReader,
    CancellationToken cancellationToken) =>
{
    try
    {
        var roster = await rosterReader.GetRosterAsync(agentId, cancellationToken);
        return Results.Ok(roster);
    }
    catch (ArgumentException ex)
    {
        return CreateRosterErrorResult(ex);
    }
});

app.MapGet("/api/players/{sleeperPlayerId}/availability", async (
    string sleeperPlayerId,
    IRosterReader rosterReader,
    CancellationToken cancellationToken) =>
{
    try
    {
        var availability = await rosterReader.GetPlayerAvailabilityAsync(sleeperPlayerId, cancellationToken);
        return availability is null ? Results.NotFound() : Results.Ok(availability);
    }
    catch (ArgumentException ex)
    {
        return CreateRosterErrorResult(ex);
    }
});

app.MapPost("/api/rosters/{agentId}/players/{sleeperPlayerId}", async (
    string agentId,
    string sleeperPlayerId,
    string? acquisitionSource,
    IRosterWriter rosterWriter,
    CancellationToken cancellationToken) =>
{
    try
    {
        var player = await rosterWriter.AddPlayerToRosterAsync(
            agentId,
            sleeperPlayerId,
            string.IsNullOrWhiteSpace(acquisitionSource) ? "manual" : acquisitionSource,
            cancellationToken);

        return Results.Ok(player);
    }
    catch (ArgumentException ex)
    {
        return CreateRosterErrorResult(ex);
    }
    catch (RosterPlayerNotFoundException ex)
    {
        return CreateRosterErrorResult(ex);
    }
    catch (RosterConflictException ex)
    {
        return CreateRosterErrorResult(ex);
    }
});

app.MapDelete("/api/rosters/{agentId}/players/{sleeperPlayerId}", async (
    string agentId,
    string sleeperPlayerId,
    IRosterWriter rosterWriter,
    CancellationToken cancellationToken) =>
{
    try
    {
        var player = await rosterWriter.RemovePlayerFromRosterAsync(
            agentId,
            sleeperPlayerId,
            cancellationToken);

        return Results.Ok(player);
    }
    catch (ArgumentException ex)
    {
        return CreateRosterErrorResult(ex);
    }
    catch (RosterPlayerNotFoundException ex)
    {
        return CreateRosterErrorResult(ex);
    }
    catch (RosterConflictException ex)
    {
        return CreateRosterErrorResult(ex);
    }
});

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
    string? sortBy,
    bool? sortDescending,
    int? limit,
    IPlayerCatalogReader playerCatalogReader,
    CancellationToken cancellationToken) =>
{
    var query = BuildPlayerQuery(
        name,
        team,
        position,
        byeWeek,
        sortBy,
        sortDescending,
        limit);

    var players = await playerCatalogReader.QueryAsync(query, cancellationToken);
    return Results.Ok(players);
});

app.MapGet("/api/sync/sleeper/latest", async (
    IPlayerCatalogPersistence playerCatalogPersistence,
    CancellationToken cancellationToken) =>
{
    var state = await playerCatalogPersistence.GetLatestSyncStateAsync(cancellationToken);
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
