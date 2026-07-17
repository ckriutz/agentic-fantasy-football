using System.Text.Json;
using Azure;
using Azure.Storage.Blobs;
using ModelContextProtocol.Server;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using LeagueAPI.Configuration;
using LeagueAPI.Data;
using LeagueAPI.HostedServices;
using LeagueAPI.Models;
using LeagueAPI.Services;
using LeagueAPI.Tools;

var builder = WebApplication.CreateBuilder(args);

var allowedCorsOrigins = (builder.Configuration["CORS_ALLOWED_ORIGINS"] ?? "http://localhost:3000,http://localhost:5173")
    .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

builder.Services.Configure<YahooOAuthOptions>(
    builder.Configuration.GetSection(YahooOAuthOptions.SectionName));

builder.Services.Configure<YahooSyncOptions>(
    builder.Configuration.GetSection(YahooSyncOptions.SectionName));

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(allowedCorsOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddMemoryCache();

builder.Services.AddHttpClient("YahooOAuth");
builder.Services.AddHttpClient("YahooFantasyApi");

var connectionString = builder.Configuration["DBConnectionString"];
var azureStorageConnectionString = builder.Configuration["AZURE_STORAGE_CONNECTION_STRING"];

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "DBConnectionString is required. Set it in configuration or via the DBConnectionString environment variable to point at your Postgres database.");
}

if (string.IsNullOrWhiteSpace(azureStorageConnectionString))
{
    throw new InvalidOperationException(
        "AZURE_STORAGE_CONNECTION_STRING is required. Set it to the Azure Storage account that contains FantasyPros snapshots.");
}

builder.Services.AddDbContextFactory<LeagueApiDbContext>(options =>
{
    options.UseNpgsql(connectionString);
});

builder.Services.AddSingleton(new BlobServiceClient(azureStorageConnectionString));
builder.Services.AddSingleton<PostgresYahooAuthStateStore>();
builder.Services.AddSingleton<SportsDataPlayerSyncService>();
builder.Services.AddSingleton<SportsDataSnapshotImportService>();
builder.Services.AddSingleton<FantasyProsSnapshotImportService>();
builder.Services.AddSingleton<SleeperSnapshotImportService>();
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

builder.Services.AddSingleton<PostgresAgentProfileStore>();
builder.Services.AddSingleton<IAgentProfileReader>(serviceProvider =>
    serviceProvider.GetRequiredService<PostgresAgentProfileStore>());
builder.Services.AddSingleton<IAgentProfileWriter>(serviceProvider =>
    serviceProvider.GetRequiredService<PostgresAgentProfileStore>());

builder.Services.AddSingleton<PostgresLeagueStateService>();
builder.Services.AddSingleton<ILeagueStateService>(serviceProvider =>
    serviceProvider.GetRequiredService<PostgresLeagueStateService>());

builder.Services.AddSingleton<PostgresScheduleService>();
builder.Services.AddSingleton<IScheduleService>(serviceProvider =>
    serviceProvider.GetRequiredService<PostgresScheduleService>());

builder.Services.AddSingleton<MatchupScoringService>();
builder.Services.AddSingleton<IMatchupScoringService>(serviceProvider =>
    serviceProvider.GetRequiredService<MatchupScoringService>());

builder.Services.AddSingleton<PostgresPlayerGameLockService>();
builder.Services.AddSingleton<IPlayerGameLockService>(serviceProvider =>
    serviceProvider.GetRequiredService<PostgresPlayerGameLockService>());

builder.Services.AddSingleton<PostgresWaiverService>();
builder.Services.AddSingleton<IWaiverService>(serviceProvider =>
    serviceProvider.GetRequiredService<PostgresWaiverService>());

builder.Services.AddSingleton<PostgresPlayerCatalogStore>();
builder.Services.AddSingleton<IPlayerCatalogReader>(serviceProvider =>
    serviceProvider.GetRequiredService<PostgresPlayerCatalogStore>());
builder.Services.AddSingleton<IPlayerCatalogPersistence>(serviceProvider =>
    serviceProvider.GetRequiredService<PostgresPlayerCatalogStore>());

builder.Services.AddHostedService<NightlyYahooSyncService>();

builder.Services.AddMcpServer()
    .WithHttpTransport(options => options.Stateless = true)
    .WithTools<PlayerCatalogTools>()
    .WithTools<YahooReadTools>()
    .WithTools<RosterTools>()
    .WithTools<WaiverTools>()
    .WithTools<AgentProfileTools>()
    .WithTools<LeagueStateTools>()
    .WithTools<LeagueTools>();

var app = builder.Build();

app.UseCors();

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
        "/api/rosters/{agentId}/players/{sleeperPlayerId}/slot?slotType=",
        "/api/rosters/{agentId}/lineup/auto",
        "/api/sync/sleeper/latest",
        "/api/sync/sleeper (POST: containerName, blobName, retrievedAtUtc)",
        "/api/sync/sportsdata/latest",
        "/api/sync/sportsdata (POST: containerName, blobName, retrievedAtUtc)",
        "/api/sync/fantasypros (POST: containerName, blobName, season, week, retrievedAtUtc)",
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
        "/api/agent-profiles?enabledOnly=",
        "/api/agent-profiles/{agentId}",
        "/api/agent-profiles/{agentId}/team-name",
        "/api/agent-profiles/{agentId}/bootstrap-status",
        "/api/league/schedule (POST: generate, GET: list all, ?force=true on POST to regenerate)",
        "/api/league/schedule/{week} (GET: list one week)",
        "/api/league/state",
        "/api/decisions (POST: log a decision, GET: list all with ?agentId=&type=&week=&limit=)",
        "/api/decisions/{agentId} (GET: list decisions for agent)",
        "/api/league/waivers/priority (GET: priority order)",
        "/api/league/waivers/priority/seed (POST: seed from draft order, ?force=true to reset)",
        "/api/league/waivers/{season}/{week} (GET: claims, ?agentId= to filter)",
        "/api/league/waivers/{season}/{week}/agents/{agentId}/summary",
        "/api/league/waivers/{season}/{week}/claims (POST: submit prioritized claim list)",
        "/api/league/waivers/{season}/{week}/process (POST: run waiver processing)",
        "/api/league/waivers/{season}/{week}/status (GET: has week been processed?)",
        "/api/league/free-agents/{season}/{week}/add (POST: immediate free-agent add/drop)"
    }
}));

// --- Agent Profiles ---

app.MapGet("/api/agent-profiles", async (
    bool? enabledOnly,
    IAgentProfileReader agentProfileReader,
    CancellationToken cancellationToken) =>
{
    var profiles = await agentProfileReader.GetAgentProfilesAsync(enabledOnly ?? true, cancellationToken);
    return Results.Ok(profiles);
});

app.MapGet("/api/agent-profiles/{agentId}", async (
    string agentId,
    IAgentProfileReader agentProfileReader,
    CancellationToken cancellationToken) =>
{
    try
    {
        var profile = await agentProfileReader.GetAgentProfileAsync(agentId, cancellationToken);
        return profile is null ? Results.NotFound() : Results.Ok(profile);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapPut("/api/agent-profiles/{agentId}", async (
    string agentId,
    UpsertAgentProfileRequest request,
    IAgentProfileWriter agentProfileWriter,
    CancellationToken cancellationToken) =>
{
    try
    {
        var profile = await agentProfileWriter.UpsertAgentProfileAsync(
            agentId,
            request.ModelName,
            request.Connection,
            request.TeamName,
            request.IsBootstrapped,
            request.IsEnabled,
            cancellationToken);

        return Results.Ok(profile);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapPatch("/api/agent-profiles/{agentId}/team-name", async (
    string agentId,
    SetAgentTeamNameRequest request,
    IAgentProfileWriter agentProfileWriter,
    CancellationToken cancellationToken) =>
{
    try
    {
        var profile = await agentProfileWriter.SetTeamNameAsync(agentId, request.TeamName, cancellationToken);
        return profile is null ? Results.NotFound() : Results.Ok(profile);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapPatch("/api/agent-profiles/{agentId}/bootstrap-status", async (
    string agentId,
    SetAgentBootstrapStatusRequest request,
    IAgentProfileWriter agentProfileWriter,
    CancellationToken cancellationToken) =>
{
    try
    {
        var profile = await agentProfileWriter.SetBootstrapStatusAsync(agentId, request.IsBootstrapped, cancellationToken);
        return profile is null ? Results.NotFound() : Results.Ok(profile);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

// --- League State ---

static IResult CreateScheduleErrorResult(Exception exception)
{
    return exception switch
    {
        ArgumentException ex => Results.BadRequest(new { error = ex.Message }),
        InvalidOperationException ex => Results.Conflict(new { error = ex.Message }),
        _ => Results.Problem(exception.Message)
    };
}

app.MapPost("/api/league/schedule", async (
    bool? force,
    IScheduleService scheduleService,
    CancellationToken cancellationToken) =>
{
    try
    {
        var result = await scheduleService.GenerateScheduleAsync(force ?? false, cancellationToken);
        return Results.Ok(result);
    }
    catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
    {
        return CreateScheduleErrorResult(ex);
    }
});

app.MapGet("/api/league/schedule", async (
    IScheduleService scheduleService,
    CancellationToken cancellationToken) =>
{
    var schedule = await scheduleService.GetScheduleAsync(cancellationToken);
    return Results.Ok(schedule);
});

app.MapGet("/api/league/schedule/{week:int}", async (
    int week,
    IScheduleService scheduleService,
    CancellationToken cancellationToken) =>
{
    try
    {
        var schedule = await scheduleService.GetScheduleForWeekAsync(week, cancellationToken);
        return Results.Ok(schedule);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapGet("/api/league/standings", async (
    IScheduleService scheduleService,
    CancellationToken cancellationToken) =>
{
    var standings = await scheduleService.GetStandingsAsync(cancellationToken);
    return Results.Ok(standings);
});

app.MapPost("/api/league/matchups/{season:int}/{week:int}/scores", async (
    int season,
    int week,
    IMatchupScoringService matchupScoringService,
    CancellationToken cancellationToken) =>
{
    try
    {
        var result = await matchupScoringService.UpdateLiveScoresAsync(season, week, cancellationToken);
        return Results.Ok(result);
    }
    catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
    {
        return CreateScheduleErrorResult(ex);
    }
});

app.MapPost("/api/league/matchups/{season:int}/{week:int}/finalize", async (
    int season,
    int week,
    IMatchupScoringService matchupScoringService,
    CancellationToken cancellationToken) =>
{
    try
    {
        var result = await matchupScoringService.FinalizeWeekAsync(season, week, cancellationToken);
        return Results.Ok(result);
    }
    catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
    {
        return CreateScheduleErrorResult(ex);
    }
});

app.MapGet("/api/league/state", async (
    ILeagueStateService leagueStateService,
    CancellationToken cancellationToken) =>
{
    var state = await leagueStateService.GetLeagueStateAsync(cancellationToken);
    return Results.Ok(state);
});

app.MapPut("/api/league/state", async (
    SetLeagueStateRequest request,
    ILeagueStateService leagueStateService,
    CancellationToken cancellationToken) =>
{
    try
    {
        var state = await leagueStateService.SetLeagueStateAsync(
            request.Season,
            request.Week,
            request.Phase,
            request.UpdatedBy,
            cancellationToken);

        return Results.Ok(state);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

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
        RosterMoveValidationException validationException when validationException.FailureType is RosterMoveFailureType.InvalidSlotType or RosterMoveFailureType.IneligibleSlot
            => Results.BadRequest(new { error = validationException.Message }),
        RosterMoveValidationException validationException when validationException.FailureType == RosterMoveFailureType.PlayerNotOnRoster
            => Results.NotFound(new { error = validationException.Message }),
        RosterMoveValidationException validationException => Results.Conflict(new { error = validationException.Message }),
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

app.MapPut("/api/rosters/{agentId}/players/{sleeperPlayerId}/slot", async (
    string agentId,
    string sleeperPlayerId,
    string slotType,
    IRosterWriter rosterWriter,
    CancellationToken cancellationToken) =>
{
    try
    {
        var player = await rosterWriter.SetPlayerSlotAsync(
            agentId,
            sleeperPlayerId,
            slotType,
            cancellationToken);

        return Results.Ok(player);
    }
    catch (Exception ex) when (ex is ArgumentException or RosterMoveValidationException or RosterPlayerNotFoundException or RosterConflictException)
    {
        return CreateRosterErrorResult(ex);
    }
});

app.MapPost("/api/rosters/{agentId}/lineup/auto", async (
    string agentId,
    IRosterWriter rosterWriter,
    CancellationToken cancellationToken) =>
{
    try
    {
        var roster = await rosterWriter.AutoSetLineupAsync(agentId, cancellationToken);
        return Results.Ok(roster);
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

app.MapPost("/api/sync/sleeper", async (SleeperSnapshotImportRequest request, SleeperSnapshotImportService sleeperSnapshotImportService, CancellationToken cancellationToken) =>
{
    try
    {
        var result = await sleeperSnapshotImportService.ImportAsync(request, cancellationToken);
        return Results.Ok(result);
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
    catch (InvalidDataException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
    catch (JsonException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
    catch (RequestFailedException exception) when (exception.Status == StatusCodes.Status404NotFound)
    {
        return Results.NotFound(new { error = exception.Message });
    }
});

app.MapGet("/api/sync/sportsdata/latest", async (
    SportsDataPlayerSyncService sportsDataPlayerSyncService,
    CancellationToken cancellationToken) =>
{
    var state = await sportsDataPlayerSyncService.GetLatestSyncRunAsync(cancellationToken);
    return state is null ? Results.NotFound() : Results.Ok(state);
});

app.MapPost("/api/sync/sportsdata", async (SportsDataSnapshotImportRequest request, SportsDataSnapshotImportService sportsDataSnapshotImportService, CancellationToken cancellationToken) =>
{
    try
    {
        var result = await sportsDataSnapshotImportService.ImportAsync(request, cancellationToken);
        return Results.Ok(result);
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
    catch (InvalidDataException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
    catch (JsonException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
    catch (RequestFailedException exception) when (exception.Status == StatusCodes.Status404NotFound)
    {
        return Results.NotFound(new { error = exception.Message });
    }
});

app.MapPost("/api/sync/fantasypros", async (FantasyProsSnapshotImportRequest request, FantasyProsSnapshotImportService fantasyProsSnapshotImportService, CancellationToken cancellationToken) =>
{
    try
    {
        var result = await fantasyProsSnapshotImportService.ImportAsync(request, cancellationToken);
        return Results.Ok(result);
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
    catch (InvalidDataException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
    catch (JsonException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
    catch (RequestFailedException exception) when (exception.Status == StatusCodes.Status404NotFound)
    {
        return Results.NotFound(new { error = exception.Message });
    }
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

app.MapGet("/api/yahoo/auth/callback", async (HttpRequest httpRequest, YahooOAuthService yahooOAuthService, CancellationToken cancellationToken) =>
{
    var error = httpRequest.Query["error"].ToString();
    if (!string.IsNullOrWhiteSpace(error))
    {
        var description = httpRequest.Query["error_description"].ToString();
        return Results.Content($"<html><body><h1>Yahoo authorization failed</h1><p>{error}: {description}</p></body></html>", "text/html");
    }

    var exchangeRequest = new YahooAuthorizationExchangeRequest { RedirectUrl = httpRequest.GetDisplayUrl() };

    try
    {
        await yahooOAuthService.ExchangeAuthorizationCodeAsync(exchangeRequest, cancellationToken);
        return Results.Content("<html><body><h1>Yahoo authorization complete</h1><p>Tokens have been saved. You can close this tab.</p></body></html>", "text/html");
    }
    catch (Exception ex)
    {
        return Results.Content($"<html><body><h1>Yahoo authorization error</h1><p>{ex.Message}</p></body></html>", "text/html");
    }
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

// --- Waivers ---

static IResult CreateWaiverErrorResult(Exception exception)
{
    return exception switch
    {
        ArgumentException ex => Results.BadRequest(new { error = ex.Message }),
        RosterPlayerNotFoundException ex => Results.NotFound(new { error = ex.Message }),
        RosterConflictException ex => Results.Conflict(new { error = ex.Message }),
        InvalidOperationException ex => Results.Conflict(new { error = ex.Message }),
        _ => Results.Problem(exception.Message)
    };
}

app.MapGet("/api/league/waivers/priority", async (
    IWaiverService waiverService,
    CancellationToken cancellationToken) =>
{
    var priority = await waiverService.GetWaiverPriorityAsync(cancellationToken);
    return Results.Ok(priority);
});

app.MapPost("/api/league/waivers/priority/seed", async (
    SeedWaiverPriorityRequest request,
    bool? force,
    IWaiverService waiverService,
    CancellationToken cancellationToken) =>
{
    try
    {
        await waiverService.SeedWaiverPriorityAsync(request.DraftOrder, force ?? false, cancellationToken);
        var priority = await waiverService.GetWaiverPriorityAsync(cancellationToken);
        return Results.Ok(priority);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapGet("/api/league/waivers/{season:int}/{week:int}", async (
    int season,
    int week,
    string? agentId,
    IWaiverService waiverService,
    CancellationToken cancellationToken) =>
{
    var claims = await waiverService.GetWaiverClaimsAsync(season, week, agentId, cancellationToken);
    return Results.Ok(claims);
});

app.MapPost("/api/league/waivers/{season:int}/{week:int}/claims", async (
    int season,
    int week,
    SubmitWaiverClaimsRequest request,
    IWaiverService waiverService,
    CancellationToken cancellationToken) =>
{
    try
    {
        var claims = await waiverService.SubmitWaiverClaimsAsync(
            request.AgentId, season, week, request.Claims, cancellationToken);
        return Results.Ok(claims);
    }
    catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or RosterPlayerNotFoundException or RosterConflictException)
    {
        return CreateWaiverErrorResult(ex);
    }
});

app.MapPost("/api/league/waivers/{season:int}/{week:int}/process", async (
    int season,
    int week,
    IWaiverService waiverService,
    CancellationToken cancellationToken) =>
{
    try
    {
        var result = await waiverService.ProcessWaiverClaimsAsync(season, week, cancellationToken);
        return Results.Ok(result);
    }
    catch (InvalidOperationException ex)
    {
        return Results.Conflict(new { error = ex.Message });
    }
});

app.MapGet("/api/league/waivers/{season:int}/{week:int}/status", async (
    int season,
    int week,
    IWaiverService waiverService,
    CancellationToken cancellationToken) =>
{
    var status = await waiverService.GetWaiverProcessStatusAsync(season, week, cancellationToken);
    return Results.Ok(status);
});

app.MapGet("/api/league/waivers/{season:int}/{week:int}/agents/{agentId}/summary", async (
    int season,
    int week,
    string agentId,
    IWaiverService waiverService,
    CancellationToken cancellationToken) =>
{
    try
    {
        var summary = await waiverService.GetMyWaiverStatusAsync(agentId, season, week, cancellationToken);
        return Results.Ok(summary);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapPost("/api/league/free-agents/{season:int}/{week:int}/add", async (
    int season,
    int week,
    AddFreeAgentRequest request,
    IWaiverService waiverService,
    CancellationToken cancellationToken) =>
{
    try
    {
        var result = await waiverService.AddFreeAgentAsync(
            request.AgentId, season, week, request.AddSleeperPlayerId, request.DropSleeperPlayerId, cancellationToken);
        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        return CreateWaiverErrorResult(ex);
    }
});

app.MapMcp("/mcp");

app.Run();
