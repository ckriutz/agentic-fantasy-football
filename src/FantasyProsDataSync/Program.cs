using Azure.Storage.Blobs;
using FantasyProsDataSync.Services;
using FantasyProsDataSync.Workers;

var builder = Host.CreateApplicationBuilder(args);

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
builder.Services.AddHostedService<FantasyProsSyncWorker>();
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
