using Azure.Storage.Blobs;
using SleeperSync.Services;
using SleeperSync.Workers;

var builder = Host.CreateApplicationBuilder(args);

const string defaultPlayersEndpoint = "https://api.sleeper.app/v1/players/nfl";
const string defaultLeagueApiBaseUrl = "http://localhost:5000";
const int requestTimeoutSeconds = 120;

var azureStorageConnectionString = GetRequiredEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING");
var blobContainerName = Environment.GetEnvironmentVariable("SLEEPER_STORAGE_CONTAINER_NAME") ?? "playerdata";
var playersEndpoint = Environment.GetEnvironmentVariable("SLEEPER_PLAYERS_ENDPOINT") ?? defaultPlayersEndpoint;
var leagueApiBaseUrl = GetEnvironmentUri("LEAGUE_API_BASE_URL", defaultLeagueApiBaseUrl);

builder.Services.AddSingleton(new BlobServiceClient(azureStorageConnectionString));

builder.Services.AddHttpClient("LeagueApi", httpClient =>
{
    httpClient.BaseAddress = new Uri($"{leagueApiBaseUrl.AbsoluteUri.TrimEnd('/')}/");
    httpClient.Timeout = TimeSpan.FromSeconds(requestTimeoutSeconds);
});

builder.Services.AddHttpClient("SleeperApi", httpClient =>
{
    httpClient.Timeout = TimeSpan.FromSeconds(requestTimeoutSeconds);
    httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("SleeperSync/1.0");
});

builder.Services.AddSingleton(serviceProvider => new SleeperApiClient(serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("SleeperApi"), playersEndpoint, serviceProvider.GetRequiredService<ILogger<SleeperApiClient>>()));
builder.Services.AddSingleton(serviceProvider => new SleeperSnapshotStorage(serviceProvider.GetRequiredService<BlobServiceClient>(), blobContainerName));
builder.Services.AddHostedService<SleeperSyncWorker>();

var host = builder.Build();
await host.RunAsync();

static string GetRequiredEnvironmentVariable(string name)
{
    return Environment.GetEnvironmentVariable(name) is { Length: > 0 } value ? value : throw new InvalidOperationException($"{name} is required.");
}

static Uri GetEnvironmentUri(string name, string defaultValue)
{
    var value = Environment.GetEnvironmentVariable(name) ?? defaultValue;
    return Uri.TryCreate(value, UriKind.Absolute, out var uri) ? uri : throw new InvalidOperationException($"{name} must be an absolute URI.");
}
