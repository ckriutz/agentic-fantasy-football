using System.Text.Json;
using Azure;
using Azure.Storage.Blobs;
using ModelContextProtocol.Server;
using Microsoft.EntityFrameworkCore;
using LeagueAPI.Data;
using LeagueAPI.Models;
using LeagueAPI.Services;
using LeagueAPI.Tools;

var builder = WebApplication.CreateBuilder(args);

var allowedCorsOrigins = (builder.Configuration["CORS_ALLOWED_ORIGINS"] ?? "http://localhost:3000,http://localhost:5173")
    .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

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
        "AZURE_STORAGE_CONNECTION_STRING is required. Set it to the Azure Storage account that contains provider snapshots.");
}

builder.Services.AddDbContextFactory<LeagueApiDbContext>(options =>
{
    options.UseNpgsql(connectionString);
});

builder.Services.AddSingleton(new BlobServiceClient(azureStorageConnectionString));
builder.Services.AddSingleton<SportsDataPlayerSyncService>();
builder.Services.AddSingleton<SportsDataSnapshotImportService>();
builder.Services.AddSingleton<FantasyProsSnapshotImportService>();
builder.Services.AddSingleton<FantasyProsPointsImportService>();
builder.Services.AddSingleton<SleeperSnapshotImportService>();
builder.Services.AddSingleton<ScoringService>();
builder.Services.AddSingleton<YahooSnapshotImportService>();
builder.Services.AddSingleton<YahooReadService>();
builder.Services.AddSingleton<PlayerPointsReadService>();

builder.Services.AddSingleton<RosterStore>();
builder.Services.AddSingleton<IRosterReader>(serviceProvider =>
    serviceProvider.GetRequiredService<RosterStore>());
builder.Services.AddSingleton<IRosterWriter>(serviceProvider =>
    serviceProvider.GetRequiredService<RosterStore>());

builder.Services.AddSingleton<DecisionStore>();
builder.Services.AddSingleton<IDecisionReader>(serviceProvider =>
    serviceProvider.GetRequiredService<DecisionStore>());
builder.Services.AddSingleton<IDecisionWriter>(serviceProvider =>
    serviceProvider.GetRequiredService<DecisionStore>());

builder.Services.AddSingleton<AgentProfileStore>();
builder.Services.AddSingleton<IAgentProfileReader>(serviceProvider =>
    serviceProvider.GetRequiredService<AgentProfileStore>());
builder.Services.AddSingleton<IAgentProfileWriter>(serviceProvider =>
    serviceProvider.GetRequiredService<AgentProfileStore>());

builder.Services.AddSingleton<LeagueStateService>();
builder.Services.AddSingleton<ScheduleService>();
builder.Services.AddSingleton<MatchupScoringService>();
builder.Services.AddSingleton<PlayerGameLockService>();
builder.Services.AddSingleton<WaiverService>();

builder.Services.AddSingleton<PlayerCatalogStore>();
builder.Services.AddSingleton<IPlayerCatalogReader>(serviceProvider =>
    serviceProvider.GetRequiredService<PlayerCatalogStore>());
builder.Services.AddSingleton<IPlayerCatalogPersistence>(serviceProvider =>
    serviceProvider.GetRequiredService<PlayerCatalogStore>());

builder.Services.AddMcpServer()
    .WithHttpTransport(options => options.Stateless = true)
    .WithTools<PlayerCatalogTools>()
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
        "/api/sync/fantasypros/latest",
        "/api/sync/fantasypros (POST: containerName, blobName, season, week, retrievedAtUtc)",
        "/api/sync/fantasypros/points/latest?season=",
        "/api/sync/fantasypros/points (POST: containerName, blobName, requestedSeason, servedSeason, servedScoring, endWeek, retrievedAtUtc)",
        "/api/sync/yahoo/latest",
        "/api/sync/yahoo (POST: containerName, blobName, gameKey, season, week, retrievedAtUtc)",
        "/api/yahoo/stats/{season}/{week}?position=&limit=",
        "/api/yahoo/stats/player/{sleeperPlayerId}/{season}/week/{week}",
        "/api/yahoo/stats/by-yahoo/{yahooId}/{season}/week/{week}",
        "/api/yahoo/points/{season}/{week}?templateKey=&position=&limit=",
        "/api/yahoo/points/player/{sleeperPlayerId}/{season}/week/{week}?templateKey=",
        "/api/yahoo/points/player/{sleeperPlayerId}/{season}?templateKey=",
        "/api/yahoo/points/by-yahoo/{yahooId}/{season}/week/{week}?templateKey=",
        "/api/yahoo/points/by-yahoo/{yahooId}/{season}?templateKey=",
        "/api/yahoo/scoring-templates?activeOnly=",
        "/api/points/{season}/{week}?position=&limit=",
        "/api/points/player/{sleeperPlayerId}/{season}/week/{week}",
        "/api/points/player/{sleeperPlayerId}/{season}",
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

static IResult CreateDomainErrorResult(Exception exception)
{
    return exception switch
    {
        RosterMoveValidationException ex when ex.FailureType is RosterMoveFailureType.InvalidSlotType or RosterMoveFailureType.IneligibleSlot
            => Results.BadRequest(new { error = ex.Message }),
        RosterMoveValidationException ex when ex.FailureType == RosterMoveFailureType.PlayerNotOnRoster
            => Results.NotFound(new { error = ex.Message }),
        RosterMoveValidationException ex => Results.Conflict(new { error = ex.Message }),
        FreeAgentOperationException ex when ex.FailureType is FreeAgentFailureType.AddPlayerNotFound or FreeAgentFailureType.DropPlayerNotOnRoster
            => Results.NotFound(new { error = ex.Message }),
        FreeAgentOperationException ex when ex.FailureType is FreeAgentFailureType.AddPlayerIneligible
            => Results.BadRequest(new { error = ex.Message }),
        FreeAgentOperationException ex => Results.Conflict(new { error = ex.Message }),
        ArgumentException ex => Results.BadRequest(new { error = ex.Message }),
        RosterPlayerNotFoundException ex => Results.NotFound(new { error = ex.Message }),
        RosterConflictException ex => Results.Conflict(new { error = ex.Message }),
        InvalidOperationException ex => Results.Conflict(new { error = ex.Message }),
        _ => Results.Problem(exception.Message)
    };
}

app.MapPost("/api/league/schedule", async (
    bool? force,
    ScheduleService scheduleService,
    CancellationToken cancellationToken) =>
{
    try
    {
        var result = await scheduleService.GenerateScheduleAsync(force ?? false, cancellationToken);
        return Results.Ok(result);
    }
    catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
    {
        return CreateDomainErrorResult(ex);
    }
});

app.MapGet("/api/league/schedule", async (
    ScheduleService scheduleService,
    CancellationToken cancellationToken) =>
{
    var schedule = await scheduleService.GetScheduleAsync(cancellationToken);
    return Results.Ok(schedule);
});

app.MapGet("/api/league/schedule/{week:int}", async (
    int week,
    ScheduleService scheduleService,
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
    ScheduleService scheduleService,
    CancellationToken cancellationToken) =>
{
    var standings = await scheduleService.GetStandingsAsync(cancellationToken);
    return Results.Ok(standings);
});

app.MapPost("/api/league/matchups/{season:int}/{week:int}/scores", async (
    int season,
    int week,
    MatchupScoringService matchupScoringService,
    CancellationToken cancellationToken) =>
{
    try
    {
        var result = await matchupScoringService.UpdateLiveScoresAsync(season, week, cancellationToken);
        return Results.Ok(result);
    }
    catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
    {
        return CreateDomainErrorResult(ex);
    }
});

app.MapPost("/api/league/matchups/{season:int}/{week:int}/finalize", async (
    int season,
    int week,
    MatchupScoringService matchupScoringService,
    CancellationToken cancellationToken) =>
{
    try
    {
        var result = await matchupScoringService.FinalizeWeekAsync(season, week, cancellationToken);
        return Results.Ok(result);
    }
    catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
    {
        return CreateDomainErrorResult(ex);
    }
});

app.MapGet("/api/league/state", async (
    LeagueStateService leagueStateService,
    CancellationToken cancellationToken) =>
{
    var state = await leagueStateService.GetLeagueStateAsync(cancellationToken);
    return Results.Ok(state);
});

app.MapPut("/api/league/state", async (
    SetLeagueStateRequest request,
    LeagueStateService leagueStateService,
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
        return CreateDomainErrorResult(ex);
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
        return CreateDomainErrorResult(ex);
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
    catch (Exception ex) when (ex is ArgumentException or RosterPlayerNotFoundException or RosterConflictException)
    {
        return CreateDomainErrorResult(ex);
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
    catch (Exception ex) when (ex is ArgumentException or RosterPlayerNotFoundException or RosterConflictException)
    {
        return CreateDomainErrorResult(ex);
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
        return CreateDomainErrorResult(ex);
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
    catch (Exception ex) when (ex is ArgumentException or RosterPlayerNotFoundException or RosterConflictException)
    {
        return CreateDomainErrorResult(ex);
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
    catch (Exception exception) when (exception is ArgumentException or InvalidDataException or JsonException)
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
    catch (Exception exception) when (exception is ArgumentException or InvalidDataException or JsonException)
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
    catch (Exception exception) when (exception is ArgumentException or InvalidDataException or JsonException)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
    catch (RequestFailedException exception) when (exception.Status == StatusCodes.Status404NotFound)
    {
        return Results.NotFound(new { error = exception.Message });
    }
});

app.MapGet("/api/sync/fantasypros/latest", async (FantasyProsSnapshotImportService fantasyProsSnapshotImportService, CancellationToken cancellationToken) =>
{
    var state = await fantasyProsSnapshotImportService.GetLatestSyncRunAsync(cancellationToken);
    return state is null ? Results.NotFound() : Results.Ok(state);
});

app.MapPost("/api/sync/fantasypros/points", async (FantasyProsPointsImportRequest request, FantasyProsPointsImportService fantasyProsPointsImportService, CancellationToken cancellationToken) =>
{
    try
    {
        var result = await fantasyProsPointsImportService.ImportAsync(request, cancellationToken);
        return Results.Ok(result);
    }
    catch (Exception exception) when (exception is ArgumentException or InvalidDataException or JsonException)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
    catch (RequestFailedException exception) when (exception.Status == StatusCodes.Status404NotFound)
    {
        return Results.NotFound(new { error = exception.Message });
    }
});

app.MapGet("/api/sync/fantasypros/points/latest", async (int? season, FantasyProsPointsImportService fantasyProsPointsImportService, CancellationToken cancellationToken) =>
{
    var state = await fantasyProsPointsImportService.GetLatestSyncRunAsync(season, cancellationToken);
    return state is null ? Results.NotFound() : Results.Ok(state);
});

app.MapGet("/api/sync/yahoo/latest", async (
    string? gameKey,
    int? season,
    int? week,
    YahooSnapshotImportService yahooSnapshotImportService,
    CancellationToken cancellationToken) =>
{
    var state = await yahooSnapshotImportService.GetLatestSyncRunAsync(gameKey, season, week, cancellationToken);
    return state is null ? Results.NotFound() : Results.Ok(state);
});

app.MapPost("/api/sync/yahoo", async (YahooSnapshotImportRequest request, YahooSnapshotImportService yahooSnapshotImportService, CancellationToken cancellationToken) =>
{
    try
    {
        return Results.Ok(await yahooSnapshotImportService.ImportAsync(request, cancellationToken));
    }
    catch (Exception exception) when (exception is ArgumentException or InvalidDataException or JsonException)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
    catch (RequestFailedException exception) when (exception.Status == StatusCodes.Status404NotFound)
    {
        return Results.NotFound(new { error = exception.Message });
    }
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

// --- FantasyPros points (read surface) ---

app.MapGet("/api/points/{season:int}/{week:int}", async (
    int season,
    int week,
    string? position,
    int? limit,
    PlayerPointsReadService playerPointsReadService,
    CancellationToken cancellationToken) =>
{
    var points = await playerPointsReadService.GetWeeklyPointsAsync(
        season,
        week,
        position,
        limit ?? 25,
        cancellationToken);

    return Results.Ok(points);
});

app.MapGet("/api/points/player/{sleeperPlayerId}/{season:int}/week/{week:int}", async (
    string sleeperPlayerId,
    int season,
    int week,
    PlayerPointsReadService playerPointsReadService,
    CancellationToken cancellationToken) =>
{
    var point = await playerPointsReadService.GetPlayerWeeklyPointsAsync(
        sleeperPlayerId,
        season,
        week,
        cancellationToken);

    return point is null ? Results.NotFound() : Results.Ok(point);
});

app.MapGet("/api/points/player/{sleeperPlayerId}/{season:int}", async (
    string sleeperPlayerId,
    int season,
    PlayerPointsReadService playerPointsReadService,
    CancellationToken cancellationToken) =>
{
    var seasonPoints = await playerPointsReadService.GetPlayerSeasonPointsAsync(
        sleeperPlayerId,
        season,
        cancellationToken);

    return seasonPoints is null ? Results.NotFound() : Results.Ok(seasonPoints);
});

// --- Waivers ---

app.MapGet("/api/league/waivers/priority", async (
    WaiverService waiverService,
    CancellationToken cancellationToken) =>
{
    var priority = await waiverService.GetWaiverPriorityAsync(cancellationToken);
    return Results.Ok(priority);
});

app.MapPost("/api/league/waivers/priority/seed", async (
    SeedWaiverPriorityRequest request,
    bool? force,
    WaiverService waiverService,
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
    WaiverService waiverService,
    CancellationToken cancellationToken) =>
{
    var claims = await waiverService.GetWaiverClaimsAsync(season, week, agentId, cancellationToken);
    return Results.Ok(claims);
});

app.MapPost("/api/league/waivers/{season:int}/{week:int}/claims", async (
    int season,
    int week,
    SubmitWaiverClaimsRequest request,
    WaiverService waiverService,
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
        return CreateDomainErrorResult(ex);
    }
});

app.MapPost("/api/league/waivers/{season:int}/{week:int}/process", async (
    int season,
    int week,
    WaiverService waiverService,
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
    WaiverService waiverService,
    CancellationToken cancellationToken) =>
{
    var status = await waiverService.GetWaiverProcessStatusAsync(season, week, cancellationToken);
    return Results.Ok(status);
});

app.MapGet("/api/league/waivers/{season:int}/{week:int}/agents/{agentId}/summary", async (
    int season,
    int week,
    string agentId,
    WaiverService waiverService,
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
    WaiverService waiverService,
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
        return CreateDomainErrorResult(ex);
    }
});

app.MapMcp("/mcp");

app.Run();
