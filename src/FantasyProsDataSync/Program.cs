using Azure.Storage.Blobs;
using FantasyProsDataSync.Configuration;
using FantasyProsDataSync.Services;
using FantasyProsDataSync.Workers;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddOptions<FantasyProsSyncOptions>()
    .Bind(builder.Configuration.GetSection(FantasyProsSyncOptions.SectionName))
    .Configure(options => options.ApiKey = builder.Configuration["FANTASY_PROS_API_KEY"] ?? options.ApiKey)
    .Configure(options => options.AzureStorageConnectionString = builder.Configuration["AZURE_STORAGE_CONNECTION_STRING"] ?? options.AzureStorageConnectionString)
    .Configure(options => options.BlobContainerName = builder.Configuration["FANTASYPROS_STORAGE_CONTAINER_NAME"] ?? options.BlobContainerName)
    .Validate(options => options.ScheduleHour is >= 0 and <= 23, "FantasyProsSync:ScheduleHour must be between 0 and 23.")
    .Validate(options => options.ScheduleMinute is >= 0 and <= 59, "FantasyProsSync:ScheduleMinute must be between 0 and 59.")
    .Validate(options => Uri.TryCreate(options.ApiBaseUrl, UriKind.Absolute, out _), "FantasyProsSync:ApiBaseUrl must be an absolute URI.")
    .Validate(options => Uri.TryCreate(options.LeagueApiBaseUrl, UriKind.Absolute, out _), "FantasyProsSync:LeagueApiBaseUrl must be an absolute URI.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.ApiKey), "FANTASY_PROS_API_KEY is required.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.AzureStorageConnectionString), "AZURE_STORAGE_CONNECTION_STRING is required.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.BlobContainerName), "FANTASYPROS_STORAGE_CONTAINER_NAME is required.")
    .ValidateOnStart();

builder.Services.AddSingleton(serviceProvider =>
{
    var options = serviceProvider.GetRequiredService<IOptions<FantasyProsSyncOptions>>().Value;
    return new BlobServiceClient(options.AzureStorageConnectionString);
});

builder.Services.AddHttpClient("LeagueApi", (serviceProvider, httpClient) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<FantasyProsSyncOptions>>().Value;
    httpClient.BaseAddress = new Uri($"{options.LeagueApiBaseUrl.TrimEnd('/')}/");
    httpClient.Timeout = TimeSpan.FromSeconds(options.RequestTimeoutSeconds);
});

builder.Services.AddHttpClient<FantasyProsApiClient>((serviceProvider, httpClient) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<FantasyProsSyncOptions>>().Value;
    httpClient.BaseAddress = new Uri($"{options.ApiBaseUrl.TrimEnd('/')}/");
    httpClient.Timeout = TimeSpan.FromSeconds(options.RequestTimeoutSeconds);
    httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("FantasyProsDataSync/1.0");
    httpClient.DefaultRequestHeaders.Add("x-api-key", options.ApiKey);
});

builder.Services.AddSingleton<FantasyProsSnapshotStorage>();
builder.Services.AddHostedService<FantasyProsSyncWorker>();

var host = builder.Build();
await host.RunAsync();
