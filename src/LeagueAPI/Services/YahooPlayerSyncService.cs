using System.Globalization;
using System.Text.Json.Nodes;
using LeagueAPI.Configuration;
using LeagueAPI.Data;
using LeagueAPI.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LeagueAPI.Services;

public sealed class YahooPlayerSyncService(
    YahooFantasyApiClient yahooFantasyApiClient,
    ScoringService scoringService,
    IDbContextFactory<LeagueApiDbContext> dbContextFactory,
    IOptions<YahooSyncOptions> yahooSyncOptions,
    ILogger<YahooPlayerSyncService> logger)
{
    private static readonly IReadOnlyDictionary<int, string> DefaultStatNames = new Dictionary<int, string>
    {
        [0] = "Games Played",
        [1] = "Passing Attempts",
        [2] = "Completions",
        [3] = "Incomplete Passes",
        [4] = "Passing Yards",
        [5] = "Passing Touchdowns",
        [6] = "Interceptions",
        [7] = "Sacks",
        [8] = "Rushing Attempts",
        [9] = "Rushing Yards",
        [10] = "Rushing Touchdowns",
        [11] = "Receptions",
        [12] = "Receiving Yards",
        [13] = "Receiving Touchdowns",
        [14] = "Return Yards",
        [15] = "Return Touchdowns",
        [16] = "2-Point Conversions",
        [17] = "Fumbles",
        [18] = "Fumbles Lost",
        [19] = "Field Goals 0-19 Yards",
        [20] = "Field Goals 20-29 Yards",
        [21] = "Field Goals 30-39 Yards",
        [22] = "Field Goals 40-49 Yards",
        [23] = "Field Goals 50+ Yards",
        [24] = "Field Goals Missed 0-19 Yards",
        [25] = "Field Goals Missed 20-29 Yards",
        [26] = "Field Goals Missed 30-39 Yards",
        [27] = "Field Goals Missed 40-49 Yards",
        [28] = "Field Goals Missed 50+ Yards",
        [29] = "Point After Attempt Made",
        [30] = "Point After Attempt Missed",
        [31] = "Points Allowed",
        [32] = "Sack",
        [33] = "Interception",
        [34] = "Fumble Recovery",
        [35] = "Touchdown",
        [36] = "Safety",
        [37] = "Block Kick",
        [38] = "Tackle Solo",
        [39] = "Tackle Assist",
        [40] = "Sack",
        [41] = "Interception",
        [42] = "Fumble Force",
        [43] = "Fumble Recovery",
        [44] = "Defensive Touchdown",
        [45] = "Safety",
        [46] = "Pass Defended",
        [47] = "Block Kick",
        [48] = "Return Yards",
        [49] = "Kickoff and Punt Return Touchdowns",
        [50] = "Points Allowed 0 points",
        [51] = "Points Allowed 1-6 points",
        [52] = "Points Allowed 7-13 points",
        [53] = "Points Allowed 14-20 points",
        [54] = "Points Allowed 21-27 points",
        [55] = "Points Allowed 28-34 points",
        [56] = "Points Allowed 35+ points",
        [57] = "Offensive Fumble Return TD",
        [58] = "Pick Sixes Thrown",
        [59] = "40+ Yard Completions",
        [60] = "40+ Yard Passing Touchdowns",
        [61] = "40+ Yard Run",
        [62] = "40+ Yard Rushing Touchdowns",
        [63] = "40+ Yard Receptions",
        [64] = "40+ Yard Receiving Touchdowns",
        [65] = "Tackles for Loss",
        [66] = "Turnover Return Yards",
        [67] = "4th Down Stops",
        [68] = "Tackles for Loss",
        [69] = "Defensive Yards Allowed",
        [70] = "Defensive Yards Allowed - Negative",
        [71] = "Defensive Yards Allowed 0-99",
        [72] = "Defensive Yards Allowed 100-199",
        [73] = "Defensive Yards Allowed 200-299",
        [74] = "Defensive Yards Allowed 300-399",
        [75] = "Defensive Yards Allowed 400-499",
        [76] = "Defensive Yards Allowed 500+",
        [77] = "Three and Outs Forced",
        [78] = "Targets",
        [79] = "Passing 1st Downs",
        [80] = "Receiving 1st Downs",
        [81] = "Rushing 1st Downs",
        [82] = "Extra Point Returned",
        [83] = "Extra Point Returned",
        [84] = "Field Goals Total Yards"
    };

    private readonly YahooFantasyApiClient _yahooFantasyApiClient = yahooFantasyApiClient;
    private readonly ScoringService _scoringService = scoringService;
    private readonly IDbContextFactory<LeagueApiDbContext> _dbContextFactory = dbContextFactory;
    private readonly YahooSyncOptions _yahooSyncOptions = yahooSyncOptions.Value;
    private readonly ILogger<YahooPlayerSyncService> _logger = logger;
    private readonly SemaphoreSlim _syncLock = new(1, 1);

    public async Task<YahooSyncRun> SyncWeeklyStatsAsync(
        string gameKey,
        int season,
        int week,
        bool force,
        CancellationToken cancellationToken)
    {
        if (!_yahooSyncOptions.Enabled)
        {
            throw new InvalidOperationException("Yahoo sync is disabled.");
        }

        if (string.IsNullOrWhiteSpace(gameKey))
        {
            throw new InvalidOperationException("A Yahoo game key is required.");
        }

        if (season <= 0)
        {
            throw new InvalidOperationException("Season must be greater than zero.");
        }

        if (week <= 0)
        {
            throw new InvalidOperationException("Week must be greater than zero.");
        }

        await _syncLock.WaitAsync(cancellationToken);

        try
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            EnsureDatabaseConfigured(dbContext);

            var nowUtc = DateTimeOffset.UtcNow;
            var latestRun = await GetLatestRunAsync(dbContext, gameKey, season, week, cancellationToken);

            if (!force
                && latestRun?.Status == "Succeeded"
                && latestRun.CompletedAtUtc?.UtcDateTime.Date == nowUtc.UtcDateTime.Date)
            {
                return new YahooSyncRun
                {
                    SyncRunId = latestRun.SyncRunId,
                    GameKey = latestRun.GameKey,
                    Season = latestRun.Season,
                    Week = latestRun.Week,
                    StartedAtUtc = latestRun.StartedAtUtc,
                    CompletedAtUtc = latestRun.CompletedAtUtc,
                    Status = "Skipped",
                    PageCount = latestRun.PageCount,
                    RecordCount = latestRun.RecordCount,
                    MatchedPlayerCount = latestRun.MatchedPlayerCount,
                    UnmatchedPlayerCount = latestRun.UnmatchedPlayerCount,
                    ErrorMessage = latestRun.ErrorMessage
                };
            }

            var syncRun = new YahooSyncRun
            {
                SyncRunId = Guid.NewGuid(),
                GameKey = gameKey,
                Season = season,
                Week = week,
                StartedAtUtc = nowUtc,
                Status = "Started"
            };

            dbContext.YahooSyncRuns.Add(syncRun);
            await dbContext.SaveChangesAsync(cancellationToken);

            try
            {
                var (players, pageCount) = await FetchAllPlayersAsync(gameKey, week, cancellationToken);
                var touchedStats = await UpsertWeeklyStatsAsync(
                    dbContext,
                    syncRun.SyncRunId,
                    gameKey,
                    season,
                    week,
                    players,
                    nowUtc,
                    cancellationToken);

                await _scoringService.RecalculatePointsAsync(dbContext, touchedStats, nowUtc, cancellationToken);

                var matchedPlayerCount = touchedStats.Count(playerStat => !string.IsNullOrWhiteSpace(playerStat.SleeperPlayerId));
                var unmatchedPlayerCount = touchedStats.Count - matchedPlayerCount;

                syncRun.CompletedAtUtc = nowUtc;
                syncRun.Status = "Succeeded";
                syncRun.PageCount = pageCount;
                syncRun.RecordCount = touchedStats.Count;
                syncRun.MatchedPlayerCount = matchedPlayerCount;
                syncRun.UnmatchedPlayerCount = unmatchedPlayerCount;
                syncRun.ErrorMessage = null;

                await dbContext.SaveChangesAsync(cancellationToken);

                _logger.LogInformation(
                    "Yahoo weekly sync completed: {SyncRunId}, game {GameKey}, season {Season}, week {Week}, fetched {FetchedCount} rows across {PageCount} pages, matched {MatchedCount}, unmatched {UnmatchedCount}.",
                    syncRun.SyncRunId,
                    gameKey,
                    season,
                    week,
                    touchedStats.Count,
                    pageCount,
                    matchedPlayerCount,
                    unmatchedPlayerCount);

                return syncRun;
            }
            catch (Exception exception)
            {
                syncRun.CompletedAtUtc = DateTimeOffset.UtcNow;
                syncRun.Status = "Failed";
                syncRun.ErrorMessage = exception.Message;

                await dbContext.SaveChangesAsync(cancellationToken);

                _logger.LogError(
                    exception,
                    "Yahoo weekly sync failed for game {GameKey}, season {Season}, week {Week}, sync run {SyncRunId}.",
                    gameKey,
                    season,
                    week,
                    syncRun.SyncRunId);

                throw;
            }
        }
        finally
        {
            _syncLock.Release();
        }
    }

    public async Task<YahooSyncRun?> GetLatestSyncRunAsync(
        string? gameKey,
        int? season,
        int? week,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        EnsureDatabaseConfigured(dbContext);
        return await GetLatestRunAsync(dbContext, gameKey, season, week, cancellationToken);
    }

    private async Task<(List<YahooParsedPlayer> players, int pageCount)> FetchAllPlayersAsync(
        string gameKey,
        int week,
        CancellationToken cancellationToken)
    {
        var pageSize = _yahooSyncOptions.PageSize is > 0 and <= 25 ? _yahooSyncOptions.PageSize : 25;
        var parsedPlayersById = new Dictionary<int, YahooParsedPlayer>();
        var start = 0;
        var pageCount = 0;

        while (true)
        {
            var payload = await _yahooFantasyApiClient.GetWeeklyPlayerStatsJsonAsync(
                gameKey,
                week,
                start,
                pageSize,
                cancellationToken);

            var pagePlayers = ParsePlayers(payload);
            if (pagePlayers.Count == 0)
            {
                break;
            }

            pageCount++;

            foreach (var player in pagePlayers)
            {
                parsedPlayersById[player.YahooPlayerId] = player;
            }

            if (pagePlayers.Count < pageSize)
            {
                break;
            }

            start += pageSize;
        }

        return (parsedPlayersById.Values.ToList(), pageCount);
    }

    private static async Task<List<WeeklyPlayerStat>> UpsertWeeklyStatsAsync(
        LeagueApiDbContext dbContext,
        Guid syncRunId,
        string gameKey,
        int season,
        int week,
        IReadOnlyCollection<YahooParsedPlayer> players,
        DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken)
    {
        var yahooPlayerIds = players.Select(player => player.YahooPlayerId).ToArray();

        var existingStatsByYahooId = await dbContext.WeeklyPlayerStats
            .Where(playerStat =>
                playerStat.GameKey == gameKey
                && playerStat.Season == season
                && playerStat.Week == week
                && yahooPlayerIds.Contains(playerStat.YahooPlayerId))
            .Include(playerStat => playerStat.StatValues)
            .Include(playerStat => playerStat.Points)
            .ToDictionaryAsync(playerStat => playerStat.YahooPlayerId, cancellationToken);

        var sleeperPlayerIdsByYahooId = (await dbContext.Players
            .AsNoTracking()
            .Where(player => player.YahooId != null && yahooPlayerIds.Contains(player.YahooId.Value))
            .Select(player => new { YahooId = player.YahooId!.Value, player.SleeperPlayerId })
            .ToListAsync(cancellationToken))
            .GroupBy(player => player.YahooId)
            .ToDictionary(group => group.Key, group => group.First().SleeperPlayerId);

        var touchedStats = new List<WeeklyPlayerStat>(players.Count);

        foreach (var player in players)
        {
            if (!existingStatsByYahooId.TryGetValue(player.YahooPlayerId, out var playerStat))
            {
                playerStat = new WeeklyPlayerStat
                {
                    GameKey = gameKey,
                    Season = season,
                    Week = week,
                    YahooPlayerId = player.YahooPlayerId,
                    RawJson = player.RawJson
                };

                dbContext.WeeklyPlayerStats.Add(playerStat);
                existingStatsByYahooId[player.YahooPlayerId] = playerStat;
            }
            else
            {
                if (playerStat.StatValues.Count > 0)
                {
                    dbContext.WeeklyPlayerStatValues.RemoveRange(playerStat.StatValues);
                    playerStat.StatValues.Clear();
                }
            }

            playerStat.SleeperPlayerId = sleeperPlayerIdsByYahooId.GetValueOrDefault(player.YahooPlayerId);
            playerStat.FullName = player.FullName;
            playerStat.Team = player.Team;
            playerStat.Position = player.Position;
            playerStat.EditorialTeamAbbr = player.EditorialTeamAbbr;
            playerStat.SyncRunId = syncRunId;
            playerStat.RawJson = player.RawJson;
            playerStat.UpdatedAtUtc = updatedAtUtc;

            foreach (var statValue in player.StatValues)
            {
                playerStat.StatValues.Add(new WeeklyPlayerStatValue
                {
                    StatId = statValue.StatId,
                    StatName = statValue.StatName,
                    Value = statValue.Value
                });
            }

            touchedStats.Add(playerStat);
        }

        return touchedStats;
    }

    private static List<YahooParsedPlayer> ParsePlayers(string payload)
    {
        var root = JsonNode.Parse(payload)
            ?? throw new InvalidOperationException("Yahoo returned an empty player stats response.");

        var playersNode = FindFirstProperty(root, "players");
        if (playersNode is not JsonObject playersObj)
        {
            return [];
        }

        var result = new List<YahooParsedPlayer>();
        foreach (var entry in playersObj)
        {
            if (entry.Key == "count" || entry.Value is null)
            {
                continue;
            }

            // Each entry is {"player": [...]}, get the player container node
            var playerContainer = FindFirstProperty(entry.Value, "player") ?? entry.Value;
            var parsed = ParsePlayer(playerContainer);
            if (parsed is not null)
            {
                result.Add(parsed);
            }
        }

        return result;
    }

    private static YahooParsedPlayer? ParsePlayer(JsonNode playerNode)
    {
        if (!TryParseInt(FindFirstString(playerNode, "player_id"), out var yahooPlayerId))
        {
            return null;
        }

        var statValues = ParseStatValues(playerNode);
        if (statValues.Count == 0)
        {
            return null;
        }

        return new YahooParsedPlayer(
            yahooPlayerId,
            FindFirstString(playerNode, "full"),
            FindFirstString(playerNode, "editorial_team_full_name") ?? FindFirstString(playerNode, "editorial_team_abbr"),
            FindFirstString(playerNode, "display_position"),
            FindFirstString(playerNode, "editorial_team_abbr"),
            playerNode.ToJsonString(),
            statValues);
    }

    private static List<YahooParsedStatValue> ParseStatValues(JsonNode playerNode)
    {
        var statsNode = FindFirstProperty(playerNode, "stats");
        if (statsNode is null)
        {
            return [];
        }

        return EnumerateMatchingObjects(
                statsNode,
                static node => node.ContainsKey("stat_id") && node.ContainsKey("value"))
            .Select(ParseStatValue)
            .Where(statValue => statValue is not null)
            .Cast<YahooParsedStatValue>()
            .ToList();
    }

    private static YahooParsedStatValue? ParseStatValue(JsonObject statNode)
    {
        if (!TryParseInt(FindFirstString(statNode, "stat_id"), out var statId))
        {
            return null;
        }

        if (!TryParseDecimal(FindFirstString(statNode, "value"), out var value))
        {
            return null;
        }

        var statName = FindFirstString(statNode, "display_name");
        if (string.IsNullOrWhiteSpace(statName))
        {
            statName = DefaultStatNames.TryGetValue(statId, out var defaultStatName)
                ? defaultStatName
                : null;
        }

        return new YahooParsedStatValue(statId, statName, value);
    }

    private static JsonNode? FindFirstProperty(JsonNode? node, string propertyName)
    {
        switch (node)
        {
            case JsonObject jsonObject:
                if (jsonObject.TryGetPropertyValue(propertyName, out var directMatch) && directMatch is not null)
                {
                    return directMatch;
                }

                foreach (var property in jsonObject)
                {
                    var nestedMatch = FindFirstProperty(property.Value, propertyName);
                    if (nestedMatch is not null)
                    {
                        return nestedMatch;
                    }
                }

                break;
            case JsonArray jsonArray:
                foreach (var item in jsonArray)
                {
                    var nestedMatch = FindFirstProperty(item, propertyName);
                    if (nestedMatch is not null)
                    {
                        return nestedMatch;
                    }
                }

                break;
        }

        return null;
    }

    private static IEnumerable<JsonObject> EnumerateMatchingObjects(
        JsonNode? node,
        Func<JsonObject, bool> predicate)
    {
        switch (node)
        {
            case JsonObject jsonObject when predicate(jsonObject):
                yield return jsonObject;
                yield break;
            case JsonObject jsonObject:
                foreach (var property in jsonObject)
                {
                    if (property.Key == "count" || property.Value is null)
                    {
                        continue;
                    }

                    foreach (var child in EnumerateMatchingObjects(property.Value, predicate))
                    {
                        yield return child;
                    }
                }

                break;
            case JsonArray jsonArray:
                foreach (var item in jsonArray)
                {
                    foreach (var child in EnumerateMatchingObjects(item, predicate))
                    {
                        yield return child;
                    }
                }

                break;
        }
    }

    private static string? FindFirstString(JsonNode? node, string propertyName)
    {
        var propertyValue = FindFirstProperty(node, propertyName);
        return propertyValue is null ? null : ConvertNodeToString(propertyValue);
    }

    private static string? ConvertNodeToString(JsonNode node)
    {
        if (node is JsonValue jsonValue)
        {
            if (jsonValue.TryGetValue<string>(out var stringValue))
            {
                return stringValue;
            }

            if (jsonValue.TryGetValue<int>(out var intValue))
            {
                return intValue.ToString(CultureInfo.InvariantCulture);
            }

            if (jsonValue.TryGetValue<long>(out var longValue))
            {
                return longValue.ToString(CultureInfo.InvariantCulture);
            }

            if (jsonValue.TryGetValue<decimal>(out var decimalValue))
            {
                return decimalValue.ToString(CultureInfo.InvariantCulture);
            }

            if (jsonValue.TryGetValue<double>(out var doubleValue))
            {
                return doubleValue.ToString(CultureInfo.InvariantCulture);
            }
        }

        return null;
    }

    private static bool TryParseInt(string? value, out int parsedValue) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsedValue);

    private static bool TryParseDecimal(string? value, out decimal parsedValue) =>
        decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsedValue);

    private static void EnsureDatabaseConfigured(LeagueApiDbContext dbContext)
    {
        if (string.IsNullOrWhiteSpace(dbContext.Database.ProviderName))
        {
            throw new InvalidOperationException(
                "Yahoo sync requires ConnectionStrings:LeagueAPI to be configured and the database migrations to be applied.");
        }
    }

    private static Task<YahooSyncRun?> GetLatestRunAsync(
        LeagueApiDbContext dbContext,
        string? gameKey,
        int? season,
        int? week,
        CancellationToken cancellationToken)
    {
        var query = dbContext.YahooSyncRuns.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(gameKey))
        {
            query = query.Where(syncRun => syncRun.GameKey == gameKey);
        }

        if (season.HasValue)
        {
            query = query.Where(syncRun => syncRun.Season == season.Value);
        }

        if (week.HasValue)
        {
            query = query.Where(syncRun => syncRun.Week == week.Value);
        }

        return query
            .OrderByDescending(syncRun => syncRun.StartedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private sealed record YahooParsedPlayer(
        int YahooPlayerId,
        string? FullName,
        string? Team,
        string? Position,
        string? EditorialTeamAbbr,
        string RawJson,
        List<YahooParsedStatValue> StatValues);

    private sealed record YahooParsedStatValue(int StatId, string? StatName, decimal Value);
}
