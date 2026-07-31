using Azure.Storage.Blobs;
using FantasyProsDataSync.Services;
using FantasyProsDataSync.Workers;

var builder = WebApplication.CreateBuilder(args);

const string fantasyProsApiBaseUrl = "https://api.fantasypros.com/public/v2/json";
const string defaultLeagueApiBaseUrl = "http://localhost:5000";
const int requestTimeoutSeconds = 30;

var fantasyProsApiKey = GetRequiredEnvironmentVariable("FANTASY_PROS_API_KEY");
var azureStorageConnectionString = GetRequiredEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING");
var blobContainerName = Environment.GetEnvironmentVariable("FANTASYPROS_STORAGE_CONTAINER_NAME") ?? "playerdata";
var leagueApiBaseUrl = GetEnvironmentUri("LEAGUE_API_BASE_URL", defaultLeagueApiBaseUrl);

builder.Services.AddSingleton(new BlobServiceClient(azureStorageConnectionString));

builder.Services.AddHttpClient("LeagueApi", httpClient =>
{
    httpClient.BaseAddress = new Uri($"{leagueApiBaseUrl.AbsoluteUri.TrimEnd('/')}/");
    httpClient.Timeout = TimeSpan.FromSeconds(requestTimeoutSeconds);
});

builder.Services.AddHttpClient<FantasyProsApiClient>(httpClient =>
{
    httpClient.BaseAddress = new Uri($"{fantasyProsApiBaseUrl.TrimEnd('/')}/");
    httpClient.Timeout = TimeSpan.FromSeconds(requestTimeoutSeconds);
    httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("FantasyProsDataSync/1.0");
    httpClient.DefaultRequestHeaders.Add("x-api-key", fantasyProsApiKey);
});

builder.Services.AddSingleton(serviceProvider => new FantasyProsSnapshotStorage(serviceProvider.GetRequiredService<BlobServiceClient>(), blobContainerName));
builder.Services.AddSingleton<FantasyProsPointsSyncOrchestrator>();
builder.Services.AddHostedService<FantasyProsSyncWorker>();
builder.Services.AddHostedService<FantasyProsPointsSyncWorker>();

var allowedCorsOrigins = (builder.Configuration["CORS_ALLOWED_ORIGINS"]
    ?? "http://localhost:5173")
    .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy => policy.WithOrigins(allowedCorsOrigins).AllowAnyHeader().AllowAnyMethod());
});

var app = builder.Build();

app.UseCors();

app.MapGet("/", () => Results.Ok(new
{
    service = "FantasyProsDataSync",
    endpoints = new[]
    {
        "/api/sync/fantasypros/points (POST)"
    }
}));

app.MapPost("/api/sync/fantasypros/points", async (int? endWeek, FantasyProsPointsSyncOrchestrator orchestrator, ILogger<Program> logger, CancellationToken cancellationToken) =>
{
    try
    {
        return Results.Ok(await orchestrator.RunAsync(endWeek, cancellationToken));
    }
    catch (InvalidOperationException exception)
    {
        return Results.Conflict(new { error = exception.Message });
    }
    catch (HttpRequestException exception)
    {
        logger.LogError(exception, "Manual FantasyPros points sync failed.");
        return Results.Problem("FantasyPros points sync request failed. Inspect FantasyProsDataSync logs for details.", statusCode: StatusCodes.Status502BadGateway);
    }
    catch (Exception exception)
    {
        logger.LogError(exception, "Manual FantasyPros points sync failed.");
        return Results.Problem("FantasyPros points sync failed. Inspect FantasyProsDataSync logs for details.", statusCode: StatusCodes.Status500InternalServerError);
    }
});

await app.RunAsync();

static string GetRequiredEnvironmentVariable(string name)
{
    return Environment.GetEnvironmentVariable(name) is { Length: > 0 } value ? value : throw new InvalidOperationException($"{name} is required.");
}

static Uri GetEnvironmentUri(string name, string defaultValue)
{
    var value = Environment.GetEnvironmentVariable(name) ?? defaultValue;
    return Uri.TryCreate(value, UriKind.Absolute, out var uri) ? uri : throw new InvalidOperationException($"{name} must be an absolute URI.");
}
