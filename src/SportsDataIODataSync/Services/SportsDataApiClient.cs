using System.Text.Json;
using SportsDataIODataSync.Models;

namespace SportsDataIODataSync.Services;

public sealed class SportsDataApiClient(HttpClient httpClient, string apiKey, string fantasyPlayersEndpoint, ILogger<SportsDataApiClient> logger)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient = httpClient;
    private readonly string _apiKey = apiKey;
    private readonly string _fantasyPlayersEndpoint = fantasyPlayersEndpoint.TrimStart('/');
    private readonly ILogger<SportsDataApiClient> _logger = logger;

    public async Task<SportsDataPlayersSnapshot> GetFantasyPlayersSnapshotAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            throw new InvalidOperationException("SPORTSDATA_API_KEY must be configured.");
        }

        var requestUri = $"{_fantasyPlayersEndpoint}?key={Uri.EscapeDataString(_apiKey)}";
        _logger.LogInformation("Requesting SportsDataIO fantasy players from {RequestUri}.", _fantasyPlayersEndpoint);

        using var response = await _httpClient.GetAsync(requestUri, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        var players = JsonSerializer.Deserialize<List<SportsDataFantasyPlayer>>(payload, SerializerOptions) ?? throw new InvalidOperationException("SportsDataIO returned an invalid fantasy players response.");

        return new SportsDataPlayersSnapshot(DateTimeOffset.UtcNow, players);
    }
}
