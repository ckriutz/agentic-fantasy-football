using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using YahooDataSync.Configuration;

namespace YahooDataSync.Services;

internal sealed class YahooFantasyApiClient(IHttpClientFactory httpClientFactory, YahooOAuthService oauthService, YahooOAuthOptions options)
{
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly YahooOAuthService _oauthService = oauthService;
    private readonly YahooOAuthOptions _options = options;

    internal Task<JsonElement> GetGameInfoAsync(CancellationToken cancellationToken)
    {
        return GetAsync("game/nfl", cancellationToken);
    }

    internal Task<JsonElement> GetLeagueSettingsAsync(string leagueKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(leagueKey))
        {
            throw new ArgumentException("A Yahoo league key is required.", nameof(leagueKey));
        }

        return GetAsync($"league/{Uri.EscapeDataString(leagueKey.Trim())}/settings", cancellationToken);
    }

    internal async Task<string> GetGameKeyAsync(int season, CancellationToken cancellationToken)
    {
        var payload = await GetAsync($"games;game_codes=nfl;seasons={season}", cancellationToken);
        var root = JsonNode.Parse(payload.GetRawText()) ?? throw new InvalidDataException("Yahoo returned an empty games response.");
        var gameKey = FindFirstString(root, "game_key");
        var gameSeason = FindFirstString(root, "season");
        if (string.IsNullOrWhiteSpace(gameKey) || !int.TryParse(gameSeason, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedSeason) || parsedSeason != season)
        {
            throw new InvalidDataException($"Yahoo did not return an NFL game key for season {season}.");
        }

        return gameKey;
    }

    internal Task<JsonElement> GetWeeklyPlayerStatsAsync(string gameKey, int week, int start, int count, CancellationToken cancellationToken)
    {
        return GetAsync($"game/{Uri.EscapeDataString(gameKey)}/players;start={start};count={count}/stats;type=week;week={week}", cancellationToken);
    }

    internal static int CountPlayers(JsonElement payload)
    {
        if (!TryFindProperty(payload, "players", out var players) || players.ValueKind != JsonValueKind.Object)
        {
            return 0;
        }

        return players.EnumerateObject().Count(property => !string.Equals(property.Name, "count", StringComparison.OrdinalIgnoreCase));
    }

    private async Task<JsonElement> GetAsync(string relativePath, CancellationToken cancellationToken)
    {
        var accessToken = await _oauthService.GetValidAccessTokenAsync(cancellationToken);
        using var response = await SendGetAsync(relativePath, accessToken, cancellationToken);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return await ReadSuccessfulJsonAsync(response, cancellationToken);
        }

        accessToken = await _oauthService.ForceRefreshAccessTokenAsync(cancellationToken);
        using var retryResponse = await SendGetAsync(relativePath, accessToken, cancellationToken);
        return await ReadSuccessfulJsonAsync(retryResponse, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendGetAsync(string relativePath, string accessToken, CancellationToken cancellationToken)
    {
        var requestUri = $"{_options.FantasyApiBaseUrl.TrimEnd('/')}/{relativePath.TrimStart('/')}?format=json";
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return await _httpClientFactory.CreateClient("YahooFantasyApi").SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
    }

    private static async Task<JsonElement> ReadSuccessfulJsonAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var diagnostic = string.Join(' ', responseBody.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            if (diagnostic.Length > 1_024)
            {
                diagnostic = diagnostic[..1_024];
            }

            throw new HttpRequestException(
                $"Yahoo API request failed with status {(int)response.StatusCode} ({response.ReasonPhrase}). Response: {diagnostic}",
                null,
                response.StatusCode);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return document.RootElement.Clone();
    }

    private static string? FindFirstString(JsonNode? node, string propertyName)
    {
        switch (node)
        {
            case JsonObject jsonObject:
                if (jsonObject.TryGetPropertyValue(propertyName, out var directValue) && directValue is JsonValue jsonValue)
                {
                    return jsonValue.TryGetValue<string>(out var stringValue) ? stringValue : jsonValue.ToJsonString().Trim('"');
                }

                foreach (var property in jsonObject)
                {
                    var nestedValue = FindFirstString(property.Value, propertyName);
                    if (nestedValue is not null)
                    {
                        return nestedValue;
                    }
                }

                break;
            case JsonArray jsonArray:
                foreach (var item in jsonArray)
                {
                    var nestedValue = FindFirstString(item, propertyName);
                    if (nestedValue is not null)
                    {
                        return nestedValue;
                    }
                }

                break;
        }

        return null;
    }

    private static bool TryFindProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty(propertyName, out value))
            {
                return true;
            }

            foreach (var property in element.EnumerateObject())
            {
                if (TryFindProperty(property.Value, propertyName, out value))
                {
                    return true;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (TryFindProperty(item, propertyName, out value))
                {
                    return true;
                }
            }
        }

        value = default;
        return false;
    }
}
