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
builder.Services.AddSingleton<PlayoffService>();
builder.Services.AddSingleton<StageAwareFinalizationService>();
builder.Services.AddSingleton<PlayoffEliminationService>();
builder.Services.AddSingleton<PlayerGameLockService>();
builder.Services.AddSingleton<WaiverService>();
builder.Services.AddSingleton<RosterMoveService>();

builder.Services.AddSingleton<PlayerCatalogStore>();
builder.Services.AddSingleton<IPlayerCatalogReader>(serviceProvider =>
    serviceProvider.GetRequiredService<PlayerCatalogStore>());
builder.Services.AddSingleton<IPlayerCatalogPersistence>(serviceProvider =>
    serviceProvider.GetRequiredService<PlayerCatalogStore>());

builder.Services.AddMcpServer()
    .WithHttpTransport(options => options.Stateless = true)
    .WithTools<PlayerCatalogTools>()
    .WithTools<RosterTools>()
    .WithTools<RosterMoveTools>()
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
        "/api/players?name=&team=&position=&byeWeek=&sortBy=&sortDescending=&limit=",
        "/api/players/available?name=&team=&position=&byeWeek=&limit=",
        "/api/rosters/{agentId}",
        "/api/league/roster-moves (POST: agentId, addSleeperPlayerId, dropSleeperPlayerId)",
        "/api/sync/sleeper/latest",
        "/api/sync/sleeper (POST: containerName, blobName, retrievedAtUtc)",
        "/api/sync/sportsdata/latest",
        "/api/sync/sportsdata (POST: containerName, blobName, retrievedAtUtc)",
        "/api/sync/fantasypros/latest",
        "/api/sync/fantasypros (POST: containerName, blobName, season, week, retrievedAtUtc)",
        "/api/sync/fantasypros/points/latest?season=",
        "/api/sync/fantasypros/points (POST: containerName, blobName, requestedSeason, servedSeason, servedScoring, endWeek, retrievedAtUtc)",
        "/api/points/player/{sleeperPlayerId}/{season}",
        "/api/agent-profiles?enabledOnly=",
        "/api/agent-profiles/{agentId}",
        "/api/agent-profiles/{agentId}/team-name",
        "/api/agent-profiles/{agentId}/bootstrap-status",
        "/api/league/seasons/{season}/schedule (POST: generate, GET: list all, ?force=true on POST to regenerate)",
        "/api/league/seasons/{season}/schedule/{week} (GET: list one week)",
        "/api/league/seasons/{season}/standings (GET: regular-season standings)",
        "/api/league/seasons/{season}/playoffs/bracket (GET: projected playoff bracket; returns the locked bracket once playoffs begin)",
        "/api/league/seasons/{season}/playoffs/eligibility (GET: active vs eliminated agents for the season)",
        "/api/league/seasons/{season}/weeks/{week}/finalize (POST: finalize the week; locks/advances playoffs and completes the season after the championship week)",
        "/api/league/state",
        "/api/decisions (POST: log a decision, GET: list all with ?agentId=&type=&week=&limit=)",
        "/api/decisions/{agentId} (GET: list decisions for agent)",
        "/api/league/waivers/priority (GET: priority order)",
        "/api/league/waivers/priority/seed (POST: seed from draft order, ?force=true to reset)",
        "/api/league/waivers/{season}/{week} (GET: claims, ?agentId= to filter)",
        "/api/league/waivers/{season}/{week}/process (POST: run waiver processing)",
        "/api/league/waivers/{season}/{week}/status (GET: has week been processed?)"
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
        return Results.Ok(profile);
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
        return Results.Ok(profile);
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

static async Task<IResult> GenerateScheduleForSeasonAsync(int season, bool force, ScheduleService scheduleService, CancellationToken cancellationToken)
{
    try
    {
        var result = await scheduleService.GenerateScheduleAsync(season, force, cancellationToken);
        return Results.Ok(result);
    }
    catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
    {
        return CreateDomainErrorResult(ex);
    }
}

static async Task<IResult> GetScheduleForSeasonAsync(int season, ScheduleService scheduleService, CancellationToken cancellationToken)
{
    try
    {
        var schedule = await scheduleService.GetScheduleAsync(season, cancellationToken);
        return Results.Ok(schedule);
    }
    catch (ArgumentException ex)
    {
        return CreateDomainErrorResult(ex);
    }
}

static async Task<IResult> GetScheduleForSeasonWeekAsync(int season, int week, ScheduleService scheduleService, CancellationToken cancellationToken)
{
    try
    {
        var schedule = await scheduleService.GetScheduleForWeekAsync(season, week, cancellationToken);
        return Results.Ok(schedule);
    }
    catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
    {
        return CreateDomainErrorResult(ex);
    }
}

static async Task<IResult> GetStandingsForSeasonAsync(int season, ScheduleService scheduleService, CancellationToken cancellationToken)
{
    try
    {
        var standings = await scheduleService.GetStandingsAsync(season, cancellationToken);
        return Results.Ok(standings);
    }
    catch (ArgumentException ex)
    {
        return CreateDomainErrorResult(ex);
    }
}

app.MapPost("/api/league/seasons/{season:int}/schedule", async (
    int season,
    bool? force,
    ScheduleService scheduleService,
    CancellationToken cancellationToken) =>
{
    return await GenerateScheduleForSeasonAsync(season, force ?? false, scheduleService, cancellationToken);
});

app.MapGet("/api/league/seasons/{season:int}/schedule", async (
    int season,
    ScheduleService scheduleService,
    CancellationToken cancellationToken) =>
{
    return await GetScheduleForSeasonAsync(season, scheduleService, cancellationToken);
});

app.MapGet("/api/league/seasons/{season:int}/schedule/{week:int}", async (
    int season,
    int week,
    ScheduleService scheduleService,
    CancellationToken cancellationToken) =>
{
    return await GetScheduleForSeasonWeekAsync(season, week, scheduleService, cancellationToken);
});

app.MapGet("/api/league/seasons/{season:int}/standings", async (
    int season,
    ScheduleService scheduleService,
    CancellationToken cancellationToken) =>
{
    return await GetStandingsForSeasonAsync(season, scheduleService, cancellationToken);
});

app.MapGet("/api/league/seasons/{season:int}/playoffs/bracket", async (
    int season,
    PlayoffService playoffService,
    CancellationToken cancellationToken) =>
{
    try
    {
        var bracket = await playoffService.GetBracketAsync(season, cancellationToken);
        return Results.Ok(bracket);
    }
    catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
    {
        return CreateDomainErrorResult(ex);
    }
});

app.MapGet("/api/league/seasons/{season:int}/playoffs/eligibility", async (
    int season,
    PlayoffEliminationService eliminationService,
    CancellationToken cancellationToken) =>
{
    try
    {
        var eligibility = await eliminationService.GetEligibilityAsync(season, cancellationToken);
        return Results.Ok(eligibility);
    }
    catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
    {
        return CreateDomainErrorResult(ex);
    }
});

app.MapPost("/api/league/seasons/{season:int}/weeks/{week:int}/finalize", async (
    int season,
    int week,
    StageAwareFinalizationService finalizationService,
    CancellationToken cancellationToken) =>
{
    try
    {
        var result = await finalizationService.FinalizeWeekAsync(season, week, LeagueStateUpdatedBy.SeasonRunner, cancellationToken);
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
            request.SeasonStage,
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

app.MapPost("/api/league/roster-moves", async (
    MakeRosterMoveRequest request,
    RosterMoveService rosterMoveService,
    CancellationToken cancellationToken) =>
{
    try
    {
        var result = await rosterMoveService.MakeRosterMoveAsync(
            request.AgentId,
            request.AddSleeperPlayerId,
            request.DropSleeperPlayerId,
            request.AcquisitionSource,
            cancellationToken);

        return Results.Ok(result);
    }
    catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
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

// --- FantasyPros points (read surface) ---

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

app.MapMcp("/mcp");

app.Run();
