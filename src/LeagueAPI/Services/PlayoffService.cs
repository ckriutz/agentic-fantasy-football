using LeagueAPI.Data;
using LeagueAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace LeagueAPI.Services;

public sealed class PlayoffService(IDbContextFactory<LeagueApiDbContext> dbContextFactory, ScheduleService scheduleService)
{
    private const long BracketLockKey = 55003;

    private readonly IDbContextFactory<LeagueApiDbContext> _dbContextFactory = dbContextFactory;
    private readonly ScheduleService _scheduleService = scheduleService;

    public async Task<PlayoffBracketResult> GetBracketAsync(int season, CancellationToken cancellationToken)
    {
        var lockedBracket = await TryGetLockedBracketAsync(season, cancellationToken);
        if (lockedBracket is not null)
            return lockedBracket;

        return await GetProjectedBracketAsync(season, cancellationToken);
    }

    public async Task<PlayoffBracketResult?> TryGetLockedBracketAsync(int season, CancellationToken cancellationToken)
    {
        ValidateSeason(season);

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var bracket = await dbContext.PlayoffBrackets
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.Season == season, cancellationToken);

        if (bracket is null || bracket.Status == PlayoffBracketStatuses.Projected)
            return null;

        var core = await LoadBracketCoreAsync(dbContext, bracket, cancellationToken);
        var settings = await GetSettingsAsync(dbContext, cancellationToken);

        return new PlayoffBracketResult(
            season,
            core.Status,
            settings.RegularSeasonEndWeek,
            settings.PlayoffStartWeek,
            settings.ChampionshipWeek,
            settings.PlayoffTeamCount,
            settings.FirstRoundByeCount,
            core.Seeds,
            core.Games);
    }

    public async Task<PlayoffBracketResult> GetProjectedBracketAsync(int season, CancellationToken cancellationToken)
    {
        if (season <= 0)
            throw new ArgumentException("season must be a positive integer.", nameof(season));

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var settings = await GetSettingsAsync(dbContext, cancellationToken);
        var (standings, completedMatchups) = await LoadStandingsContextAsync(dbContext, season, cancellationToken);
        var (rankedStandings, seeds) = ComputeSeeds(settings, standings, completedMatchups);

        return new PlayoffBracketResult(
            season,
            PlayoffBracketStatuses.Projected,
            settings.RegularSeasonEndWeek,
            settings.PlayoffStartWeek,
            settings.ChampionshipWeek,
            settings.PlayoffTeamCount,
            settings.FirstRoundByeCount,
            seeds,
            BuildProjectedGames(settings, seeds));
    }

    public async Task<LockPlayoffBracketResult> LockBracketAsync(int season, string updatedBy, CancellationToken cancellationToken)
    {
        ValidateSeason(season);

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({BracketLockKey})", cancellationToken);

        var existingLocked = await TryLoadLockedBracketCoreAsync(dbContext, season, cancellationToken);
        if (existingLocked is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return new LockPlayoffBracketResult(season, existingLocked.BracketId, existingLocked.Status, false, existingLocked.Seeds, existingLocked.Games);
        }

        var settings = await GetSettingsAsync(dbContext, cancellationToken);
        ValidateSupportedSettings(settings);

        var endWeekMatchups = await dbContext.Matchups
            .Where(matchup => matchup.Season == season && matchup.Week == settings.RegularSeasonEndWeek && matchup.MatchupType == MatchupTypes.RegularSeason)
            .ToListAsync(cancellationToken);

        if (endWeekMatchups.Count == 0)
            throw new InvalidOperationException($"Cannot lock the playoff bracket for season {season} because no regular-season matchups exist for week {settings.RegularSeasonEndWeek}.");

        if (endWeekMatchups.Any(matchup => !matchup.IsComplete))
            throw new InvalidOperationException($"Cannot lock the playoff bracket for season {season} until every week {settings.RegularSeasonEndWeek} regular-season matchup is complete.");

        var (standings, completedMatchups) = await LoadStandingsContextAsync(dbContext, season, cancellationToken);
        var (rankedStandings, _) = ComputeSeeds(settings, standings, completedMatchups);
        var rankedAgentIds = rankedStandings.Select(standing => standing.AgentId).ToList();
        if (rankedAgentIds.Count < settings.PlayoffTeamCount)
            throw new InvalidOperationException($"Cannot lock the playoff bracket for season {season}: only {rankedAgentIds.Count} teams have standings but {settings.PlayoffTeamCount} playoff teams are configured.");

        var bracket = await dbContext.PlayoffBrackets.FirstOrDefaultAsync(row => row.Season == season, cancellationToken);
        if (bracket is null)
        {
            bracket = new PlayoffBracketEntity { Season = season };
            dbContext.PlayoffBrackets.Add(bracket);
        }
        else
        {
            RemoveBracketChildren(dbContext, bracket.Id);
            bracket.Status = PlayoffBracketStatuses.Projected;
            bracket.UpdatedAtUtc = DateTimeOffset.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var seedResults = rankedAgentIds
            .Take(settings.PlayoffTeamCount)
            .Select((agentId, index) =>
            {
                var standing = rankedStandings[index];
                var result = new PlayoffSeedResult(index + 1, agentId, standing.Wins, standing.Losses, standing.Ties, standing.WinningPercentage, standing.PointsFor, standing.PointsAgainst, index < settings.FirstRoundByeCount);
                dbContext.PlayoffSeeds.Add(new PlayoffSeedEntity
                {
                    BracketId = bracket!.Id,
                    Seed = result.Seed,
                    AgentId = result.AgentId,
                    Wins = result.Wins,
                    Losses = result.Losses,
                    Ties = result.Ties,
                    WinningPercentage = result.WinningPercentage,
                    PointsFor = result.PointsFor,
                    PointsAgainst = result.PointsAgainst
                });
                return result;
            })
            .ToList();

        var wildCard1 = new PlayoffBracketGameEntity { BracketId = bracket.Id, Round = PlayoffRounds.WildCard, GameSlot = 1, Week = settings.PlayoffStartWeek, HomeSeed = 3, AwaySeed = 6, HomeAgentId = AgentForSeed(seedResults, 3), AwayAgentId = AgentForSeed(seedResults, 6), Status = PlayoffGameStatuses.Scheduled };
        var wildCard2 = new PlayoffBracketGameEntity { BracketId = bracket.Id, Round = PlayoffRounds.WildCard, GameSlot = 2, Week = settings.PlayoffStartWeek, HomeSeed = 4, AwaySeed = 5, HomeAgentId = AgentForSeed(seedResults, 4), AwayAgentId = AgentForSeed(seedResults, 5), Status = PlayoffGameStatuses.Scheduled };
        var semifinal1 = new PlayoffBracketGameEntity { BracketId = bracket.Id, Round = PlayoffRounds.Semifinal, GameSlot = 1, Week = settings.PlayoffStartWeek + 1, HomeSeed = 1, HomeAgentId = AgentForSeed(seedResults, 1), AwaySourceOutcome = PlayoffParticipantSources.Winner, Status = PlayoffGameStatuses.Pending };
        var semifinal2 = new PlayoffBracketGameEntity { BracketId = bracket.Id, Round = PlayoffRounds.Semifinal, GameSlot = 2, Week = settings.PlayoffStartWeek + 1, HomeSeed = 2, HomeAgentId = AgentForSeed(seedResults, 2), AwaySourceOutcome = PlayoffParticipantSources.Winner, Status = PlayoffGameStatuses.Pending };
        var championship = new PlayoffBracketGameEntity { BracketId = bracket.Id, Round = PlayoffRounds.Championship, GameSlot = 1, Week = settings.ChampionshipWeek, HomeSourceOutcome = PlayoffParticipantSources.Winner, AwaySourceOutcome = PlayoffParticipantSources.Winner, Status = PlayoffGameStatuses.Pending };
        var thirdPlace = new PlayoffBracketGameEntity { BracketId = bracket.Id, Round = PlayoffRounds.ThirdPlace, GameSlot = 1, Week = settings.ChampionshipWeek, HomeSourceOutcome = PlayoffParticipantSources.Loser, AwaySourceOutcome = PlayoffParticipantSources.Loser, Status = PlayoffGameStatuses.Pending };

        dbContext.PlayoffBracketGames.AddRange(wildCard1, wildCard2, semifinal1, semifinal2, championship, thirdPlace);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Source-game and matchup references require generated ids, so link them after the first save.
        semifinal1.AwaySourceGameId = wildCard2.Id;
        semifinal2.AwaySourceGameId = wildCard1.Id;
        championship.HomeSourceGameId = semifinal1.Id;
        championship.AwaySourceGameId = semifinal2.Id;
        thirdPlace.HomeSourceGameId = semifinal1.Id;
        thirdPlace.AwaySourceGameId = semifinal2.Id;

        var wildCardMatchup1 = new MatchupEntity { Season = season, Week = wildCard1.Week, MatchupType = MatchupTypes.Playoff, HomeAgentId = wildCard1.HomeAgentId!, AwayAgentId = wildCard1.AwayAgentId! };
        var wildCardMatchup2 = new MatchupEntity { Season = season, Week = wildCard2.Week, MatchupType = MatchupTypes.Playoff, HomeAgentId = wildCard2.HomeAgentId!, AwayAgentId = wildCard2.AwayAgentId! };
        dbContext.Matchups.AddRange(wildCardMatchup1, wildCardMatchup2);

        await dbContext.SaveChangesAsync(cancellationToken);
        wildCard1.MatchupId = wildCardMatchup1.Id;
        wildCard2.MatchupId = wildCardMatchup2.Id;

        bracket.Status = PlayoffBracketStatuses.Locked;
        bracket.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        var leagueStateEntity = await LeagueStateService.GetOrCreateLeagueStateAsync(dbContext, cancellationToken);
        leagueStateEntity.SeasonStage = SeasonStages.Playoffs;
        leagueStateEntity.UpdatedBy = updatedBy;
        leagueStateEntity.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return new LockPlayoffBracketResult(
            season,
            bracket.Id,
            bracket.Status,
            true,
            seedResults,
            new[] { wildCard1, wildCard2, semifinal1, semifinal2, championship, thirdPlace }.Select(game => MapToGameResult(game)).ToList());
    }

    private async Task<(IReadOnlyList<AgentStanding> Standings, IReadOnlyList<MatchupEntity> CompletedMatchups)> LoadStandingsContextAsync(LeagueApiDbContext dbContext, int season, CancellationToken cancellationToken)
    {
        var standings = await _scheduleService.GetStandingsAsync(season, cancellationToken);
        var completedMatchups = await dbContext.Matchups
            .AsNoTracking()
            .Where(matchup =>
                matchup.Season == season
                && matchup.MatchupType == MatchupTypes.RegularSeason
                && matchup.IsComplete)
            .ToListAsync(cancellationToken);

        return (standings, completedMatchups);
    }

    private (IReadOnlyList<AgentStanding> RankedStandings, IReadOnlyList<PlayoffSeedResult> Seeds) ComputeSeeds(PlayoffSettingsEntity settings, IReadOnlyList<AgentStanding> standings, IReadOnlyList<MatchupEntity> completedMatchups)
    {
        ValidateSupportedSettings(settings);

        var rankedStandings = RankStandings(standings, completedMatchups);
        var seeds = rankedStandings
            .Take(settings.PlayoffTeamCount)
            .Select((standing, index) => new PlayoffSeedResult(
                index + 1,
                standing.AgentId,
                standing.Wins,
                standing.Losses,
                standing.Ties,
                standing.WinningPercentage,
                standing.PointsFor,
                standing.PointsAgainst,
                index < settings.FirstRoundByeCount))
            .ToList();

        return (rankedStandings, seeds);
    }

    private static async Task<PlayoffSettingsEntity> GetSettingsAsync(LeagueApiDbContext dbContext, CancellationToken cancellationToken)
    {
        return await dbContext.PlayoffSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(settings => settings.Id == PlayoffSettingsDefaults.SingletonId, cancellationToken)
            ?? new PlayoffSettingsEntity();
    }

    private sealed record LockedBracketCore(int BracketId, string Status, IReadOnlyList<PlayoffSeedResult> Seeds, IReadOnlyList<PlayoffGameResult> Games);

    private async Task<LockedBracketCore?> TryLoadLockedBracketCoreAsync(LeagueApiDbContext dbContext, int season, CancellationToken cancellationToken)
    {
        var bracket = await dbContext.PlayoffBrackets
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.Season == season, cancellationToken);

        if (bracket is null || bracket.Status == PlayoffBracketStatuses.Projected)
            return null;

        return await LoadBracketCoreAsync(dbContext, bracket, cancellationToken);
    }

    private static async Task<LockedBracketCore> LoadBracketCoreAsync(LeagueApiDbContext dbContext, PlayoffBracketEntity bracket, CancellationToken cancellationToken)
    {
        var settings = await GetSettingsAsync(dbContext, cancellationToken);
        var seedEntities = await dbContext.PlayoffSeeds
            .AsNoTracking()
            .Where(seed => seed.BracketId == bracket.Id)
            .OrderBy(seed => seed.Seed)
            .ToListAsync(cancellationToken);
        var gameEntities = await dbContext.PlayoffBracketGames
            .AsNoTracking()
            .Where(game => game.BracketId == bracket.Id)
            .OrderBy(game => game.Round)
            .ThenBy(game => game.GameSlot)
            .ToListAsync(cancellationToken);
        var byeSeedCount = settings.FirstRoundByeCount;

        return new LockedBracketCore(
            bracket.Id,
            bracket.Status,
            seedEntities.Select(seed => MapToSeedResult(seed, seed.Seed <= byeSeedCount)).ToList(),
            gameEntities.Select(game => MapToGameResult(game)).ToList());
    }

    private static void RemoveBracketChildren(LeagueApiDbContext dbContext, int bracketId)
    {
        var games = dbContext.PlayoffBracketGames.Where(game => game.BracketId == bracketId);
        dbContext.PlayoffBracketGames.RemoveRange(games);
        var seeds = dbContext.PlayoffSeeds.Where(seed => seed.BracketId == bracketId);
        dbContext.PlayoffSeeds.RemoveRange(seeds);
    }

    private static string? AgentForSeed(IReadOnlyList<PlayoffSeedResult> seeds, int seed) => seeds.FirstOrDefault(candidate => candidate.Seed == seed)?.AgentId;

    private static PlayoffSeedResult MapToSeedResult(PlayoffSeedEntity seed, bool hasFirstRoundBye) =>
        new(seed.Seed, seed.AgentId, seed.Wins, seed.Losses, seed.Ties, seed.WinningPercentage, seed.PointsFor, seed.PointsAgainst, hasFirstRoundBye);

    private static PlayoffGameResult MapToGameResult(PlayoffBracketGameEntity game) =>
        new(
            game.Round,
            game.GameSlot,
            game.Week,
            game.HomeSeed,
            game.AwaySeed,
            game.HomeAgentId,
            game.AwayAgentId,
            DescribeSource(game.HomeSourceGameId, game.HomeSourceOutcome),
            DescribeSource(game.AwaySourceGameId, game.AwaySourceOutcome));

    private static string? DescribeSource(int? sourceGameId, string? outcome)
    {
        if (!sourceGameId.HasValue)
            return null;

        var participant = outcome == PlayoffParticipantSources.Loser ? "Loser" : "Winner";
        return $"{participant} of game {sourceGameId.Value}";
    }

    private static void ValidateSeason(int season)
    {
        if (season <= 0)
            throw new ArgumentException("season must be a positive integer.", nameof(season));
    }

    private static IReadOnlyList<AgentStanding> RankStandings(IReadOnlyList<AgentStanding> standings, IReadOnlyList<MatchupEntity> completedMatchups)
    {
        var remaining = standings.ToList();
        var ranked = new List<AgentStanding>(remaining.Count);

        while (remaining.Count > 0)
        {
            var bestWinningPercentage = remaining.Max(standing => standing.WinningPercentage);
            var candidates = remaining.Where(standing => standing.WinningPercentage == bestWinningPercentage).ToList();

            if (candidates.Count > 1)
            {
                var bestPointsFor = candidates.Max(standing => standing.PointsFor);
                candidates = candidates.Where(standing => standing.PointsFor == bestPointsFor).ToList();
            }

            if (candidates.Count > 1 && TryGetHeadToHeadWinner(candidates, completedMatchups, out var headToHeadWinner))
                candidates = [headToHeadWinner];

            if (candidates.Count > 1)
            {
                var bestPointsAgainst = candidates.Max(standing => standing.PointsAgainst);
                candidates = candidates.Where(standing => standing.PointsAgainst == bestPointsAgainst).ToList();
            }

            var selected = candidates.OrderBy(standing => standing.AgentId, StringComparer.Ordinal).First();
            ranked.Add(selected);
            remaining.Remove(selected);
        }

        return ranked;
    }

    private static bool TryGetHeadToHeadWinner(IReadOnlyList<AgentStanding> candidates, IReadOnlyList<MatchupEntity> completedMatchups, out AgentStanding winner)
    {
        winner = candidates[0];
        var candidateIds = candidates.Select(candidate => candidate.AgentId).ToHashSet(StringComparer.Ordinal);
        var pairGameCounts = new List<int>();
        for (var firstIndex = 0; firstIndex < candidates.Count; firstIndex++)
        {
            for (var secondIndex = firstIndex + 1; secondIndex < candidates.Count; secondIndex++)
            {
                var firstAgentId = candidates[firstIndex].AgentId;
                var secondAgentId = candidates[secondIndex].AgentId;
                pairGameCounts.Add(completedMatchups.Count(matchup =>
                    (string.Equals(matchup.HomeAgentId, firstAgentId, StringComparison.Ordinal)
                     && string.Equals(matchup.AwayAgentId, secondAgentId, StringComparison.Ordinal))
                    || (string.Equals(matchup.HomeAgentId, secondAgentId, StringComparison.Ordinal)
                        && string.Equals(matchup.AwayAgentId, firstAgentId, StringComparison.Ordinal))));
            }
        }

        if (pairGameCounts.Distinct().Count() != 1 || pairGameCounts[0] == 0)
            return false;

        var records = candidates.ToDictionary(
            candidate => candidate.AgentId,
            _ => new HeadToHeadRecord(),
            StringComparer.Ordinal);

        foreach (var matchup in completedMatchups.Where(matchup =>
                     candidateIds.Contains(matchup.HomeAgentId)
                     && candidateIds.Contains(matchup.AwayAgentId)))
        {
            records[matchup.HomeAgentId].Games++;
            records[matchup.AwayAgentId].Games++;

            if (matchup.IsTie)
            {
                records[matchup.HomeAgentId].Ties++;
                records[matchup.AwayAgentId].Ties++;
            }
            else if (string.Equals(matchup.WinnerAgentId, matchup.HomeAgentId, StringComparison.Ordinal))
            {
                records[matchup.HomeAgentId].Wins++;
            }
            else if (string.Equals(matchup.WinnerAgentId, matchup.AwayAgentId, StringComparison.Ordinal))
            {
                records[matchup.AwayAgentId].Wins++;
            }
        }

        var bestPercentage = records.Values.Max(record => record.WinningPercentage);
        var winners = candidates.Where(candidate => records[candidate.AgentId].WinningPercentage == bestPercentage).ToList();
        if (winners.Count != 1)
            return false;

        winner = winners[0];
        return true;
    }

    private static IReadOnlyList<PlayoffGameResult> BuildProjectedGames(PlayoffSettingsEntity settings, IReadOnlyList<PlayoffSeedResult> seeds)
    {
        PlayoffSeedResult? Seed(int seed) => seeds.FirstOrDefault(candidate => candidate.Seed == seed);

        return
        [
            CreateSeedGame(PlayoffRounds.WildCard, 1, settings.PlayoffStartWeek, Seed(3), Seed(6)),
            CreateSeedGame(PlayoffRounds.WildCard, 2, settings.PlayoffStartWeek, Seed(4), Seed(5)),
            new PlayoffGameResult(PlayoffRounds.Semifinal, 1, settings.PlayoffStartWeek + 1, 1, null, Seed(1)?.AgentId, null, null, "Winner of Wild Card 2"),
            new PlayoffGameResult(PlayoffRounds.Semifinal, 2, settings.PlayoffStartWeek + 1, 2, null, Seed(2)?.AgentId, null, null, "Winner of Wild Card 1"),
            new PlayoffGameResult(PlayoffRounds.Championship, 1, settings.ChampionshipWeek, null, null, null, null, "Winner of Semifinal 1", "Winner of Semifinal 2"),
            new PlayoffGameResult(PlayoffRounds.ThirdPlace, 1, settings.ChampionshipWeek, null, null, null, null, "Loser of Semifinal 1", "Loser of Semifinal 2")
        ];
    }

    private static PlayoffGameResult CreateSeedGame(string round, int gameSlot, int week, PlayoffSeedResult? home, PlayoffSeedResult? away)
    {
        return new PlayoffGameResult(round, gameSlot, week, home?.Seed, away?.Seed, home?.AgentId, away?.AgentId, null, null);
    }

    private static void ValidateSupportedSettings(PlayoffSettingsEntity settings)
    {
        if (settings.PlayoffTeamCount != 6 || settings.FirstRoundByeCount != 2 || settings.Reseed)
            throw new InvalidOperationException("Projected brackets currently require six playoff teams, two first-round byes, and fixed seeding.");

        if (!settings.ThirdPlaceGameEnabled)
            throw new InvalidOperationException("Projected brackets currently require the third-place game to be enabled.");

        if (settings.PlayoffStartWeek + 2 != settings.ChampionshipWeek)
            throw new InvalidOperationException("Projected brackets currently require three consecutive playoff weeks.");
    }

    private sealed class HeadToHeadRecord
    {
        public int Wins { get; set; }
        public int Ties { get; set; }
        public int Games { get; set; }
        public decimal WinningPercentage => Games == 0 ? 0m : (Wins + Ties * 0.5m) / Games;
    }
}
