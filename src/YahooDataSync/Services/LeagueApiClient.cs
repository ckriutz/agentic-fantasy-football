using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using YahooDataSync.Models;

namespace YahooDataSync.Services;

internal sealed class LeagueApiClient(IHttpClientFactory httpClientFactory)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;

    internal async Task<LeagueState> GetLeagueStateAsync(CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("LeagueApi");
        using var response = await client.GetAsync("api/league/state", HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<LeagueState>(stream, SerializerOptions, cancellationToken) ?? throw new InvalidDataException("LeagueAPI returned an invalid league state.");
    }

    internal async Task ImportYahooSnapshotAsync(YahooSnapshotImportRequest request, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("LeagueApi");
        const int maxAttempts = 3;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                using var response = await client.PostAsJsonAsync("api/sync/yahoo", request, SerializerOptions, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    return;
                }

                if (attempt < maxAttempts && response.StatusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests or >= HttpStatusCode.InternalServerError)
                {
                    await Task.Delay(TimeSpan.FromSeconds(attempt * 2), cancellationToken);
                    continue;
                }

                throw new HttpRequestException($"LeagueAPI Yahoo snapshot import failed with status {(int)response.StatusCode}.", null, response.StatusCode);
            }
            catch (HttpRequestException exception) when (exception.StatusCode is null && attempt < maxAttempts)
            {
                await Task.Delay(TimeSpan.FromSeconds(attempt * 2), cancellationToken);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && attempt < maxAttempts)
            {
                await Task.Delay(TimeSpan.FromSeconds(attempt * 2), cancellationToken);
            }
        }
    }
}
