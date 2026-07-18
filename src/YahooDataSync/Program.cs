using System.Net;
using Azure.Storage.Blobs;
using YahooDataSync.Configuration;
using YahooDataSync.Models;
using YahooDataSync.Services;
using YahooDataSync.Workers;

var builder = WebApplication.CreateBuilder(args);

const int requestTimeoutSeconds = 120;

var azureStorageConnectionString = GetRequiredEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING");
var leagueApiBaseUrl = GetEnvironmentUri("LEAGUE_API_BASE_URL", "http://192.168.40.159:8082");
var storageOptions = new YahooStorageOptions
{
    ContainerName = builder.Configuration["YAHOO_STORAGE_CONTAINER_NAME"] ?? "yahoodata",
    OAuthStateBlobName = builder.Configuration["YAHOO_OAUTH_STATE_BLOB_NAME"] ?? "yahoo/private/oauth-state.json"
};
var oauthOptions = new YahooOAuthOptions
{
    ClientId = GetRequiredEnvironmentVariable("YAHOO_CLIENT_ID", "YahooClientId"),
    ClientSecret = GetRequiredEnvironmentVariable("YAHOO_CLIENT_SECRET", "YahooClientSecret"),
    RedirectUri = GetRequiredEnvironmentVariable("YAHOO_REDIRECT_URI"),
    AuthorizationUrl = builder.Configuration["YahooOAuth:AuthorizationUrl"] ?? "https://api.login.yahoo.com/oauth2/request_auth",
    TokenUrl = builder.Configuration["YahooOAuth:TokenUrl"] ?? "https://api.login.yahoo.com/oauth2/get_token",
    FantasyApiBaseUrl = builder.Configuration["YahooOAuth:FantasyApiBaseUrl"] ?? "https://fantasysports.yahooapis.com/fantasy/v2",
    Scope = builder.Configuration["YahooOAuth:Scope"] ?? "fspt-r"
};
var syncOptions = builder.Configuration.GetSection(YahooSyncOptions.SectionName).Get<YahooSyncOptions>() ?? new YahooSyncOptions();

ValidateOptions(storageOptions, oauthOptions, syncOptions);

builder.Services.AddSingleton(storageOptions);
builder.Services.AddSingleton(oauthOptions);
builder.Services.AddSingleton(syncOptions);
builder.Services.AddSingleton(new BlobServiceClient(azureStorageConnectionString));

builder.Services.AddHttpClient("YahooOAuth", httpClient =>
{
    httpClient.Timeout = TimeSpan.FromSeconds(requestTimeoutSeconds);
    httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("YahooDataSync/1.0");
});
builder.Services.AddHttpClient("YahooFantasyApi", httpClient =>
{
    httpClient.Timeout = TimeSpan.FromSeconds(requestTimeoutSeconds);
    httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("YahooDataSync/1.0");
});
builder.Services.AddHttpClient("LeagueApi", httpClient =>
{
    httpClient.BaseAddress = new Uri($"{leagueApiBaseUrl.AbsoluteUri.TrimEnd('/')}/");
    httpClient.Timeout = TimeSpan.FromSeconds(requestTimeoutSeconds);
});

builder.Services.AddSingleton<YahooOAuthStateStore>();
builder.Services.AddSingleton<YahooOAuthService>();
builder.Services.AddSingleton<YahooFantasyApiClient>();
builder.Services.AddSingleton<YahooSnapshotStorage>();
builder.Services.AddSingleton<LeagueApiClient>();
builder.Services.AddSingleton<YahooSyncOrchestrator>();
builder.Services.AddHostedService<YahooSyncWorker>();

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
    service = "YahooDataSync",
    endpoints = new[]
    {
        "/api/yahoo/auth/status",
        "/api/yahoo/auth/authorize",
        "/api/yahoo/auth/authorize-url",
        "/api/yahoo/auth/callback",
        "/api/yahoo/auth/exchange",
        "/api/yahoo/auth/refresh",
        "/api/yahoo/auth/test-connection",
        "/api/yahoo/league/{leagueKey}/settings/raw",
        "/api/sync/yahoo (POST)"
    }
}));

app.MapGet("/api/yahoo/auth/status", async (YahooOAuthService yahooOAuthService, CancellationToken cancellationToken) =>
{
    return Results.Ok(await yahooOAuthService.GetStatusAsync(cancellationToken));
});

app.MapGet("/api/yahoo/auth/authorize", async (YahooOAuthService yahooOAuthService, CancellationToken cancellationToken) =>
{
    var response = await yahooOAuthService.CreateAuthorizationUrlAsync(cancellationToken);
    return Results.Redirect(response.AuthorizationUrl);
});

app.MapPost("/api/yahoo/auth/authorize-url", async (YahooOAuthService yahooOAuthService, CancellationToken cancellationToken) =>
{
    return Results.Ok(await yahooOAuthService.CreateAuthorizationUrlAsync(cancellationToken));
});

app.MapPost("/api/yahoo/auth/exchange", async (YahooAuthorizationExchangeRequest request, YahooOAuthService yahooOAuthService, CancellationToken cancellationToken) =>
{
    try
    {
        return Results.Ok(await yahooOAuthService.ExchangeAuthorizationCodeAsync(request, cancellationToken));
    }
    catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or YahooAuthStateConcurrencyException)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
});

app.MapGet("/api/yahoo/auth/callback", async (HttpRequest request, YahooOAuthService yahooOAuthService, CancellationToken cancellationToken) =>
{
    try
    {
        var returnedState = request.Query["state"].ToString();
        var error = request.Query["error"].ToString();
        if (!string.IsNullOrWhiteSpace(error))
        {
            await yahooOAuthService.RejectAuthorizationAsync(returnedState, cancellationToken);
            var description = request.Query["error_description"].ToString();
            var safeMessage = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(description) ? error : $"{error}: {description}");
            return Results.Content($"<html><body><h1>Yahoo authorization failed</h1><p>{safeMessage}</p></body></html>", "text/html", statusCode: StatusCodes.Status400BadRequest);
        }

        var code = request.Query["code"].ToString();
        await yahooOAuthService.ExchangeAuthorizationCodeAsync(new YahooAuthorizationExchangeRequest(code, returnedState, null), cancellationToken);
        return Results.Content("<html><body><h1>Yahoo authorization complete</h1><p>The refresh token was saved. You can close this tab.</p></body></html>", "text/html");
    }
    catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or YahooAuthStateConcurrencyException)
    {
        var safeMessage = WebUtility.HtmlEncode(exception.Message);
        return Results.Content($"<html><body><h1>Yahoo authorization error</h1><p>{safeMessage}</p></body></html>", "text/html", statusCode: StatusCodes.Status400BadRequest);
    }
});

app.MapPost("/api/yahoo/auth/refresh", async (YahooOAuthService yahooOAuthService, CancellationToken cancellationToken) =>
{
    return Results.Ok(await yahooOAuthService.RefreshAccessTokenAsync(cancellationToken));
});

app.MapGet("/api/yahoo/auth/test-connection", async (YahooFantasyApiClient yahooFantasyApiClient, CancellationToken cancellationToken) =>
{
    var payload = await yahooFantasyApiClient.GetGameInfoAsync(cancellationToken);
    return Results.Json(payload);
});

app.MapGet("/api/yahoo/league/{leagueKey}/settings/raw", async (string leagueKey, YahooFantasyApiClient yahooFantasyApiClient, CancellationToken cancellationToken) =>
{
    var payload = await yahooFantasyApiClient.GetLeagueSettingsAsync(leagueKey, cancellationToken);
    return Results.Json(payload);
});

app.MapPost("/api/sync/yahoo", async (YahooSyncOrchestrator yahooSyncOrchestrator, CancellationToken cancellationToken) =>
{
    try
    {
        return Results.Ok(await yahooSyncOrchestrator.RunAsync(cancellationToken));
    }
    catch (InvalidOperationException exception)
    {
        return Results.Conflict(new { error = exception.Message });
    }
});

await app.RunAsync();

static string GetRequiredEnvironmentVariable(params string[] names)
{
    foreach (var name in names)
    {
        if (Environment.GetEnvironmentVariable(name) is { Length: > 0 } value)
        {
            return value;
        }
    }

    throw new InvalidOperationException($"{string.Join(" or ", names)} is required.");
}

static Uri GetEnvironmentUri(string name, string defaultValue)
{
    var value = Environment.GetEnvironmentVariable(name) ?? defaultValue;
    return Uri.TryCreate(value, UriKind.Absolute, out var uri) ? uri : throw new InvalidOperationException($"{name} must be an absolute URI.");
}

static void ValidateOptions(YahooStorageOptions storageOptions, YahooOAuthOptions oauthOptions, YahooSyncOptions syncOptions)
{
    if (string.IsNullOrWhiteSpace(storageOptions.ContainerName))
    {
        throw new InvalidOperationException("YAHOO_STORAGE_CONTAINER_NAME is required.");
    }

    if (string.IsNullOrWhiteSpace(storageOptions.OAuthStateBlobName))
    {
        throw new InvalidOperationException("YAHOO_OAUTH_STATE_BLOB_NAME is required.");
    }

    if (!Uri.TryCreate(oauthOptions.RedirectUri, UriKind.Absolute, out _))
    {
        throw new InvalidOperationException("YAHOO_REDIRECT_URI must be an absolute URI.");
    }

    EnsureAscii(oauthOptions.ClientId, "YAHOO_CLIENT_ID");
    EnsureAscii(oauthOptions.ClientSecret, "YAHOO_CLIENT_SECRET");

    if (syncOptions.PageSize is < 1 or > 25)
    {
        throw new InvalidOperationException("YahooSync:PageSize must be between 1 and 25.");
    }

    if (syncOptions.DailySyncHourUtc is < 0 or > 23 || syncOptions.DailySyncMinuteUtc is < 0 or > 59)
    {
        throw new InvalidOperationException("YahooSync schedule hour and minute must be valid UTC values.");
    }
}

static void EnsureAscii(string value, string name)
{
    if (value.Any(character => character > '\x7F'))
    {
        throw new InvalidOperationException($"{name} must contain only ASCII characters. Copy it directly from the Yahoo Developer Console.");
    }
}
