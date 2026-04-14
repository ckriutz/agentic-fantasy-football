using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using LeagueAPI.Models;

namespace LeagueAPI.Services;

public sealed class SnapshotPlayerCatalogReader(
    JsonFileSyncStateStore syncStateStore,
    IHostEnvironment hostEnvironment,
    IMemoryCache memoryCache) : IPlayerCatalogReader
{
    private readonly JsonFileSyncStateStore _syncStateStore = syncStateStore;
    private readonly IHostEnvironment _hostEnvironment = hostEnvironment;
    private readonly IMemoryCache _memoryCache = memoryCache;

    public async Task<PlayerRecord?> GetBySleeperIdAsync(string sleeperPlayerId, CancellationToken cancellationToken)
    {
        var players = await LoadPlayersAsync(cancellationToken);
        return players.FirstOrDefault(player =>
            string.Equals(player.SleeperPlayerId, sleeperPlayerId, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<PlayerRecord?> GetByYahooIdAsync(int yahooId, CancellationToken cancellationToken)
    {
        var players = await LoadPlayersAsync(cancellationToken);
        return players.FirstOrDefault(player => player.YahooId == yahooId);
    }

    public async Task<IReadOnlyList<PlayerRecord>> QueryAsync(PlayerQuery query, CancellationToken cancellationToken)
    {
        var players = await LoadPlayersAsync(cancellationToken);
        var normalizedName = PlayerRecordFactory.NormalizeName(query.Name);
        var normalizedTeam = PlayerRecordFactory.NormalizeToken(query.Team);
        var normalizedPosition = PlayerRecordFactory.NormalizeToken(query.Position);
        var limit = Math.Clamp(query.Limit, 1, 200);

        IEnumerable<PlayerRecord> filteredPlayers = players;

        if (!string.IsNullOrWhiteSpace(normalizedName))
        {
            filteredPlayers = filteredPlayers.Where(player =>
                player.SearchFullNameNormalized.Contains(normalizedName, StringComparison.Ordinal));
        }

        if (!string.IsNullOrWhiteSpace(normalizedTeam))
        {
            filteredPlayers = filteredPlayers.Where(player =>
                string.Equals(player.TeamAbbr, normalizedTeam, StringComparison.OrdinalIgnoreCase)
                || string.Equals(player.Team, normalizedTeam, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(normalizedPosition))
        {
            filteredPlayers = filteredPlayers.Where(player =>
                string.Equals(player.Position, normalizedPosition, StringComparison.OrdinalIgnoreCase)
                || player.FantasyPositions.Any(position =>
                    string.Equals(position, normalizedPosition, StringComparison.OrdinalIgnoreCase)));
        }

        if (query.ByeWeek.HasValue)
        {
            filteredPlayers = filteredPlayers.Where(player => player.ByeWeek == query.ByeWeek.Value);
        }

        if (query.MinProjectedPoints.HasValue)
        {
            filteredPlayers = filteredPlayers.Where(player =>
                player.ProjectedFantasyPoints >= query.MinProjectedPoints.Value);
        }

        if (query.MaxAverageDraftPosition.HasValue)
        {
            filteredPlayers = filteredPlayers.Where(player =>
                player.AverageDraftPosition.HasValue
                && player.AverageDraftPosition <= query.MaxAverageDraftPosition.Value);
        }

        IEnumerable<PlayerRecord> sortedPlayers = query.SortBy?.ToLowerInvariant() switch
        {
            "projectedpoints" => query.SortDescending
                ? filteredPlayers.OrderByDescending(p => p.ProjectedFantasyPoints)
                : filteredPlayers.OrderBy(p => p.ProjectedFantasyPoints),
            "adp" => query.SortDescending
                ? filteredPlayers.OrderByDescending(p => p.AverageDraftPosition)
                : filteredPlayers.OrderBy(p => p.AverageDraftPosition),
            "lastseasonpoints" => query.SortDescending
                ? filteredPlayers.OrderByDescending(p => p.LastSeasonFantasyPoints)
                : filteredPlayers.OrderBy(p => p.LastSeasonFantasyPoints),
            "auctionvalue" => query.SortDescending
                ? filteredPlayers.OrderByDescending(p => p.AuctionValue)
                : filteredPlayers.OrderBy(p => p.AuctionValue),
            _ => query.SortDescending
                ? filteredPlayers.OrderByDescending(p => p.FullName ?? p.SleeperPlayerId)
                : filteredPlayers.OrderBy(p => p.FullName ?? p.SleeperPlayerId, StringComparer.OrdinalIgnoreCase)
        };

        return sortedPlayers
            .Take(limit)
            .ToArray();
    }

    private async Task<IReadOnlyList<PlayerRecord>> LoadPlayersAsync(CancellationToken cancellationToken)
    {
        var syncState = await _syncStateStore.GetLatestStateAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(syncState.SnapshotRelativePath))
        {
            return Array.Empty<PlayerRecord>();
        }

        var cacheKey = $"snapshot-player-catalog::{syncState.SnapshotRelativePath}::{syncState.PayloadSha256}";

        var players = await _memoryCache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24);

            var snapshotPath = ResolvePath(syncState.SnapshotRelativePath!);

            if (!File.Exists(snapshotPath))
            {
                return Array.Empty<PlayerRecord>();
            }

            var json = await File.ReadAllTextAsync(snapshotPath, cancellationToken);
            var playersResponse = JsonSerializer.Deserialize<SleeperPlayersResponse>(json) ?? [];

            return playersResponse
                .Where(pair => pair.Value.Active)
                .Select(pair => PlayerRecordFactory.Create(pair.Key, pair.Value))
                .OrderBy(player => player.FullName ?? player.SleeperPlayerId, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        });

        return players ?? Array.Empty<PlayerRecord>();
    }

    private string ResolvePath(string configuredPath)
    {
        return Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(_hostEnvironment.ContentRootPath, configuredPath);
    }
}
