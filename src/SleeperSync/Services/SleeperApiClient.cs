using System.Text.Json;
using SleeperSync.Models;

namespace SleeperSync.Services;

public sealed class SleeperApiClient(HttpClient httpClient, string playersEndpoint, ILogger<SleeperApiClient> logger)
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly string _playersEndpoint = playersEndpoint;
    private readonly ILogger<SleeperApiClient> _logger = logger;

    public async Task<SleeperPlayersSnapshot> GetPlayersSnapshotAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Requesting Sleeper players from {PlayersEndpoint}.", _playersEndpoint);

        using var response = await _httpClient.GetAsync(_playersEndpoint, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Sleeper returned an invalid players response; expected a JSON object.");
        }

        return new SleeperPlayersSnapshot(DateTimeOffset.UtcNow, document.RootElement.Clone());
    }
}
