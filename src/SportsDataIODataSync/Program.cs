using Azure.Storage.Blobs;
using SportsDataIODataSync.Services;
using SportsDataIODataSync.Workers;

var builder = Host.CreateApplicationBuilder(args);

const string defaultBaseUrl = "https://api.sportsdata.io/v3/nfl/stats/json";
const string defaultFantasyPlayersEndpoint = "FantasyPlayers";
const string defaultLeagueApiBaseUrl = "http://localhost:5000";
const int requestTimeoutSeconds = 30;

var sportsDataApiKey = GetRequiredEnvironmentVariable("SPORTSDATA_API_KEY");
var azureStorageConnectionString = GetRequiredEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING");
var blobContainerName = Environment.GetEnvironmentVariable("SPORTSDATA_STORAGE_CONTAINER_NAME") ?? "playerdata";
var sportsDataBaseUrl = Environment.GetEnvironmentVariable("SPORTSDATA_BASE_URL") ?? defaultBaseUrl;
var fantasyPlayersEndpoint = Environment.GetEnvironmentVariable("SPORTSDATA_FANTASY_PLAYERS_ENDPOINT") ?? defaultFantasyPlayersEndpoint;
var leagueApiBaseUrl = GetEnvironmentUri("LEAGUE_API_BASE_URL", defaultLeagueApiBaseUrl);

builder.Services.AddSingleton(new BlobServiceClient(azureStorageConnectionString));

builder.Services.AddHttpClient("LeagueApi", httpClient =>
{
    httpClient.BaseAddress = new Uri($"{leagueApiBaseUrl.AbsoluteUri.TrimEnd('/')}/");
    httpClient.Timeout = TimeSpan.FromSeconds(requestTimeoutSeconds);
});

builder.Services.AddHttpClient("SportsDataApi", httpClient =>
{
    httpClient.BaseAddress = new Uri($"{sportsDataBaseUrl.TrimEnd('/')}/");
    httpClient.Timeout = TimeSpan.FromSeconds(requestTimeoutSeconds);
    httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("SportsDataIODataSync/1.0");
});

builder.Services.AddSingleton(serviceProvider => new SportsDataApiClient(serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("SportsDataApi"), sportsDataApiKey, fantasyPlayersEndpoint, serviceProvider.GetRequiredService<ILogger<SportsDataApiClient>>()));
builder.Services.AddSingleton(serviceProvider => new SportsDataSnapshotStorage(serviceProvider.GetRequiredService<BlobServiceClient>(), blobContainerName));
builder.Services.AddHostedService<SportsDataIOSyncWorker>();

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
