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
            core.Games,
            MapFinalPlacements(bracket));
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
            BuildProjectedGames(settings, seeds),
            null);
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

    public async Task<ResolvePlayoffRoundResult> ResolveRoundAsync(int season, int week, string updatedBy, CancellationToken cancellationToken)
    {
        ValidateSeason(season);
        if (week <= 0)
            throw new ArgumentException("week must be a positive integer.", nameof(week));

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({BracketLockKey})", cancellationToken);

        var bracket = await dbContext.PlayoffBrackets.FirstOrDefaultAsync(row => row.Season == season, cancellationToken);
        if (bracket is null || bracket.Status == PlayoffBracketStatuses.Projected)
            throw new InvalidOperationException($"Cannot resolve playoff week {week} for season {season} because the playoff bracket is not locked.");

        var settings = await GetSettingsAsync(dbContext, cancellationToken);
        ValidateSupportedSettings(settings);
        if (bracket.Status == PlayoffBracketStatuses.Complete && week != settings.ChampionshipWeek)
            throw new InvalidOperationException($"Cannot resolve playoff week {week} for season {season} because the season is already complete.");

        var games = await dbContext.PlayoffBracketGames.Where(game => game.BracketId == bracket.Id).ToListAsync(cancellationToken);
        var seeds = await dbContext.PlayoffSeeds.AsNoTracking().Where(seed => seed.BracketId == bracket.Id).ToListAsync(cancellationToken);
        var seedsByAgentId = seeds.ToDictionary(seed => seed.AgentId, seed => seed.Seed, StringComparer.Ordinal);
        var gamesById = games.ToDictionary(game => game.Id);

        var weekGames = games.Where(game => game.Week == week).OrderBy(game => game.Round).ThenBy(game => game.GameSlot).ToList();
        if (weekGames.Count == 0)
            throw new InvalidOperationException($"Cannot resolve playoff week {week} for season {season}: the locked bracket has no games in that week.");

        var matchupIds = weekGames.Where(game => game.MatchupId.HasValue).Select(game => game.MatchupId!.Value).ToList();
        if (matchupIds.Count != weekGames.Count)
            throw new InvalidOperationException($"Cannot resolve playoff week {week} for season {season} because one or more bracket games are not linked to a matchup.");

        var matchups = await dbContext.Matchups.Where(matchup => matchupIds.Contains(matchup.Id)).ToListAsync(cancellationToken);
        if (matchups.Count != matchupIds.Count)
            throw new InvalidOperationException($"Cannot resolve playoff week {week} for season {season} because a linked playoff matchup is missing.");

        var matchupsById = matchups.ToDictionary(matchup => matchup.Id);
        foreach (var game in weekGames)
            CompleteBracketGame(game, matchupsById[game.MatchupId!.Value], seedsByAgentId);

        var nextWeekGames = new List<PlayoffBracketGameEntity>();
        var createdMatchups = new List<(PlayoffBracketGameEntity Game, MatchupEntity Matchup)>();
        PlayoffFinalPlacementsResult? finalPlacements = null;
        var nextWeek = GetNextPlayoffWeek(settings, week);
        if (nextWeek.HasValue)
        {
            nextWeekGames = games.Where(game => game.Week == nextWeek.Value).OrderBy(game => game.Round).ThenBy(game => game.GameSlot).ToList();
            if (nextWeekGames.Count == 0)
                throw new InvalidOperationException($"Cannot advance playoffs after week {week} for season {season}: the locked bracket has no games for week {nextWeek.Value}.");

            var existingNextMatchups = await dbContext.Matchups
                .Where(matchup => matchup.Season == season && matchup.Week == nextWeek.Value && matchup.MatchupType == MatchupTypes.Playoff)
                .ToListAsync(cancellationToken);

            foreach (var nextGame in nextWeekGames)
            {
                PopulateGameParticipants(nextGame, gamesById, seedsByAgentId);
                var created = EnsurePlayoffMatchup(dbContext, season, nextGame, existingNextMatchups);
                if (created is not null)
                    createdMatchups.Add((nextGame, created));
            }
        }
        else if (week == settings.ChampionshipWeek)
        {
            finalPlacements = CompleteSeason(bracket, games, settings);
            var leagueStateEntity = await LeagueStateService.GetOrCreateLeagueStateAsync(dbContext, cancellationToken);
            if (leagueStateEntity.Season != season)
                throw new InvalidOperationException($"Cannot complete season {season} because the current league state is for season {leagueStateEntity.Season}.");

            leagueStateEntity.SeasonStage = SeasonStages.Complete;
            leagueStateEntity.UpdatedBy = updatedBy;
            leagueStateEntity.UpdatedAtUtc = DateTimeOffset.UtcNow;
        }

        bracket.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        foreach (var (game, matchup) in createdMatchups)
            game.MatchupId = matchup.Id;

        if (createdMatchups.Count > 0)
            await dbContext.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return new ResolvePlayoffRoundResult(
            season,
            week,
            bracket.Id,
            nextWeekGames.Count > 0,
            createdMatchups.Count > 0,
            weekGames.Select(MapToGameResult).ToList(),
            nextWeekGames.Select(MapToGameResult).ToList(),
            finalPlacements is not null,
            finalPlacements);
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
            DescribeSource(game.AwaySourceGameId, game.AwaySourceOutcome),
            game.Status,
            game.WinnerAgentId,
            game.LoserAgentId);

    private static PlayoffFinalPlacementsResult? MapFinalPlacements(PlayoffBracketEntity bracket)
    {
        if (string.IsNullOrWhiteSpace(bracket.ChampionAgentId)
            || string.IsNullOrWhiteSpace(bracket.RunnerUpAgentId)
            || string.IsNullOrWhiteSpace(bracket.ThirdPlaceAgentId)
            || string.IsNullOrWhiteSpace(bracket.FourthPlaceAgentId))
            return null;

        return new PlayoffFinalPlacementsResult(bracket.ChampionAgentId, bracket.RunnerUpAgentId, bracket.ThirdPlaceAgentId, bracket.FourthPlaceAgentId);
    }

    private static PlayoffFinalPlacementsResult CompleteSeason(PlayoffBracketEntity bracket, IReadOnlyList<PlayoffBracketGameEntity> games, PlayoffSettingsEntity settings)
    {
        var championship = games.SingleOrDefault(game => game.Week == settings.ChampionshipWeek && game.Round == PlayoffRounds.Championship)
            ?? throw new InvalidOperationException($"Cannot complete season {bracket.Season} because the championship game is missing.");
        var thirdPlace = games.SingleOrDefault(game => game.Week == settings.ChampionshipWeek && game.Round == PlayoffRounds.ThirdPlace)
            ?? throw new InvalidOperationException($"Cannot complete season {bracket.Season} because the third-place game is missing.");

        if (championship.Status != PlayoffGameStatuses.Complete || string.IsNullOrWhiteSpace(championship.WinnerAgentId) || string.IsNullOrWhiteSpace(championship.LoserAgentId))
            throw new InvalidOperationException($"Cannot complete season {bracket.Season} because the championship game is not complete.");
        if (thirdPlace.Status != PlayoffGameStatuses.Complete || string.IsNullOrWhiteSpace(thirdPlace.WinnerAgentId) || string.IsNullOrWhiteSpace(thirdPlace.LoserAgentId))
            throw new InvalidOperationException($"Cannot complete season {bracket.Season} because the third-place game is not complete.");

        var placements = new PlayoffFinalPlacementsResult(championship.WinnerAgentId, championship.LoserAgentId, thirdPlace.WinnerAgentId, thirdPlace.LoserAgentId);
        var existingPlacements = MapFinalPlacements(bracket);
        if (bracket.Status == PlayoffBracketStatuses.Complete && existingPlacements != placements)
            throw new InvalidOperationException($"Cannot complete season {bracket.Season} because its persisted final placements differ from the finalized games.");

        bracket.ChampionAgentId = placements.ChampionAgentId;
        bracket.RunnerUpAgentId = placements.RunnerUpAgentId;
        bracket.ThirdPlaceAgentId = placements.ThirdPlaceAgentId;
        bracket.FourthPlaceAgentId = placements.FourthPlaceAgentId;
        bracket.Status = PlayoffBracketStatuses.Complete;
        return placements;
    }

    private static string? DescribeSource(int? sourceGameId, string? outcome)
    {
        if (!sourceGameId.HasValue)
            return null;

        var participant = outcome == PlayoffParticipantSources.Loser ? "Loser" : "Winner";
        return $"{participant} of game {sourceGameId.Value}";
    }

    private static int? GetNextPlayoffWeek(PlayoffSettingsEntity settings, int week)
    {
        if (week == settings.PlayoffStartWeek)
            return settings.PlayoffStartWeek + 1;

        if (week == settings.PlayoffStartWeek + 1)
            return settings.ChampionshipWeek;

        return null;
    }

    private static void CompleteBracketGame(PlayoffBracketGameEntity game, MatchupEntity matchup, IReadOnlyDictionary<string, int> seedsByAgentId)
    {
        if (!matchup.IsComplete)
            throw new InvalidOperationException($"Cannot resolve {game.Round} game {game.GameSlot} because matchup {matchup.Id} is not complete.");

        if (!ParticipantsMatch(game, matchup))
            throw new InvalidOperationException($"Cannot resolve {game.Round} game {game.GameSlot} because matchup {matchup.Id} participants do not match the bracket game.");

        var (winnerAgentId, loserAgentId) = ResolvePlayoffOutcome(game, matchup, seedsByAgentId);
        if (game.Status == PlayoffGameStatuses.Complete)
        {
            if (!string.Equals(game.WinnerAgentId, winnerAgentId, StringComparison.Ordinal) || !string.Equals(game.LoserAgentId, loserAgentId, StringComparison.Ordinal))
                throw new InvalidOperationException($"Cannot resolve {game.Round} game {game.GameSlot}: it is already complete with a different winner or loser.");

            return;
        }

        game.WinnerAgentId = winnerAgentId;
        game.LoserAgentId = loserAgentId;
        game.Status = PlayoffGameStatuses.Complete;
    }

    private static (string WinnerAgentId, string LoserAgentId) ResolvePlayoffOutcome(PlayoffBracketGameEntity game, MatchupEntity matchup, IReadOnlyDictionary<string, int> seedsByAgentId)
    {
        if (!matchup.IsTie)
        {
            if (string.IsNullOrWhiteSpace(matchup.WinnerAgentId))
                throw new InvalidOperationException($"Cannot resolve {game.Round} game {game.GameSlot} because matchup {matchup.Id} has no winner.");

            var winnerAgentId = matchup.WinnerAgentId;
            var loserAgentId = string.Equals(winnerAgentId, matchup.HomeAgentId, StringComparison.Ordinal) ? matchup.AwayAgentId : matchup.HomeAgentId;
            if (string.IsNullOrWhiteSpace(loserAgentId) || string.Equals(winnerAgentId, loserAgentId, StringComparison.Ordinal))
                throw new InvalidOperationException($"Cannot resolve {game.Round} game {game.GameSlot} because the winner and loser could not be determined from matchup {matchup.Id}.");

            return (winnerAgentId, loserAgentId);
        }

        var homeSeed = ResolveParticipantSeed(game, matchup.HomeAgentId, seedsByAgentId);
        var awaySeed = ResolveParticipantSeed(game, matchup.AwayAgentId, seedsByAgentId);
        if (homeSeed is null || awaySeed is null || homeSeed == awaySeed)
            throw new InvalidOperationException($"Cannot resolve the tied {game.Round} game {game.GameSlot}: both participants need distinct persisted playoff seeds.");

        return homeSeed < awaySeed
            ? (matchup.HomeAgentId, matchup.AwayAgentId)
            : (matchup.AwayAgentId, matchup.HomeAgentId);
    }

    private static void PopulateGameParticipants(PlayoffBracketGameEntity game, IReadOnlyDictionary<int, PlayoffBracketGameEntity> gamesById, IReadOnlyDictionary<string, int> seedsByAgentId)
    {
        AssignParticipantFromSource(game, home: true, gamesById, seedsByAgentId);
        AssignParticipantFromSource(game, home: false, gamesById, seedsByAgentId);

        if (string.IsNullOrWhiteSpace(game.HomeAgentId) || string.IsNullOrWhiteSpace(game.AwayAgentId))
            throw new InvalidOperationException($"Cannot schedule {game.Round} game {game.GameSlot} because both participants are not assigned.");

        if (string.Equals(game.HomeAgentId, game.AwayAgentId, StringComparison.Ordinal))
            throw new InvalidOperationException($"Cannot schedule {game.Round} game {game.GameSlot} because both participants resolved to {game.HomeAgentId}.");
    }

    private static void AssignParticipantFromSource(PlayoffBracketGameEntity game, bool home, IReadOnlyDictionary<int, PlayoffBracketGameEntity> gamesById, IReadOnlyDictionary<string, int> seedsByAgentId)
    {
        var sourceGameId = home ? game.HomeSourceGameId : game.AwaySourceGameId;
        var sourceOutcome = home ? game.HomeSourceOutcome : game.AwaySourceOutcome;
        if (!sourceGameId.HasValue)
            return;

        if (string.IsNullOrWhiteSpace(sourceOutcome))
            throw new InvalidOperationException($"Cannot populate {game.Round} game {game.GameSlot}: a source game is configured without a source outcome.");

        if (!gamesById.TryGetValue(sourceGameId.Value, out var sourceGame))
            throw new InvalidOperationException($"Cannot populate {game.Round} game {game.GameSlot}: source game {sourceGameId.Value} was not found.");

        if (sourceGame.Status != PlayoffGameStatuses.Complete || string.IsNullOrWhiteSpace(sourceGame.WinnerAgentId) || string.IsNullOrWhiteSpace(sourceGame.LoserAgentId))
            throw new InvalidOperationException($"Cannot populate {game.Round} game {game.GameSlot} because source game {sourceGameId.Value} is not complete.");

        var agentId = sourceOutcome == PlayoffParticipantSources.Loser ? sourceGame.LoserAgentId : sourceGame.WinnerAgentId;
        var seed = ResolveParticipantSeed(sourceGame, agentId, seedsByAgentId);
        var existingAgentId = home ? game.HomeAgentId : game.AwayAgentId;
        if (!string.IsNullOrWhiteSpace(existingAgentId) && !string.Equals(existingAgentId, agentId, StringComparison.Ordinal))
            throw new InvalidOperationException($"Cannot populate {game.Round} game {game.GameSlot}: {(home ? "home" : "away")} is already {existingAgentId} but the source game resolved to {agentId}.");

        if (home)
        {
            game.HomeAgentId = agentId;
            game.HomeSeed = seed ?? game.HomeSeed;
        }
        else
        {
            game.AwayAgentId = agentId;
            game.AwaySeed = seed ?? game.AwaySeed;
        }
    }

    private static MatchupEntity? EnsurePlayoffMatchup(LeagueApiDbContext dbContext, int season, PlayoffBracketGameEntity game, IReadOnlyList<MatchupEntity> existingPlayoffMatchups)
    {
        if (game.Status == PlayoffGameStatuses.Complete)
        {
            if (game.MatchupId is not int completedMatchupId)
                throw new InvalidOperationException($"Cannot reschedule {game.Round} game {game.GameSlot} because it is already complete without a linked matchup.");

            var completedMatchup = existingPlayoffMatchups.FirstOrDefault(matchup => matchup.Id == completedMatchupId);
            if (completedMatchup is null)
                throw new InvalidOperationException($"Cannot keep {game.Round} game {game.GameSlot} complete because linked matchup {completedMatchupId} was not found.");

            if (!ParticipantsMatch(game, completedMatchup))
                throw new InvalidOperationException($"Cannot keep {game.Round} game {game.GameSlot} complete because linked matchup {completedMatchupId} has different participants.");

            return null;
        }

        if (game.MatchupId is int matchupId)
        {
            var linked = existingPlayoffMatchups.FirstOrDefault(matchup => matchup.Id == matchupId);
            if (linked is null)
                throw new InvalidOperationException($"Cannot schedule {game.Round} game {game.GameSlot} because linked matchup {matchupId} was not found.");

            if (!ParticipantsMatch(game, linked))
                throw new InvalidOperationException($"Cannot schedule {game.Round} game {game.GameSlot} because linked matchup {matchupId} has different participants.");

            game.Status = PlayoffGameStatuses.Scheduled;
            return null;
        }

        var existing = existingPlayoffMatchups.FirstOrDefault(matchup =>
            matchup.Season == season
            && matchup.Week == game.Week
            && string.Equals(matchup.HomeAgentId, game.HomeAgentId, StringComparison.Ordinal)
            && string.Equals(matchup.AwayAgentId, game.AwayAgentId, StringComparison.Ordinal));

        if (existing is not null)
        {
            game.MatchupId = existing.Id;
            game.Status = PlayoffGameStatuses.Scheduled;
            return null;
        }

        var created = new MatchupEntity
        {
            Season = season,
            Week = game.Week,
            MatchupType = MatchupTypes.Playoff,
            HomeAgentId = game.HomeAgentId!,
            AwayAgentId = game.AwayAgentId!
        };
        dbContext.Matchups.Add(created);
        game.Status = PlayoffGameStatuses.Scheduled;
        return created;
    }

    private static bool ParticipantsMatch(PlayoffBracketGameEntity game, MatchupEntity matchup) =>
        string.Equals(game.HomeAgentId, matchup.HomeAgentId, StringComparison.Ordinal)
        && string.Equals(game.AwayAgentId, matchup.AwayAgentId, StringComparison.Ordinal);

    private static int? ResolveParticipantSeed(PlayoffBracketGameEntity game, string agentId, IReadOnlyDictionary<string, int> seedsByAgentId)
    {
        if (string.Equals(game.HomeAgentId, agentId, StringComparison.Ordinal) && game.HomeSeed.HasValue)
            return game.HomeSeed;

        if (string.Equals(game.AwayAgentId, agentId, StringComparison.Ordinal) && game.AwaySeed.HasValue)
            return game.AwaySeed;

        return seedsByAgentId.TryGetValue(agentId, out var seed) ? seed : null;
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
            new PlayoffGameResult(PlayoffRounds.Semifinal, 1, settings.PlayoffStartWeek + 1, 1, null, Seed(1)?.AgentId, null, null, "Winner of Wild Card 2", PlayoffGameStatuses.Pending, null, null),
            new PlayoffGameResult(PlayoffRounds.Semifinal, 2, settings.PlayoffStartWeek + 1, 2, null, Seed(2)?.AgentId, null, null, "Winner of Wild Card 1", PlayoffGameStatuses.Pending, null, null),
            new PlayoffGameResult(PlayoffRounds.Championship, 1, settings.ChampionshipWeek, null, null, null, null, "Winner of Semifinal 1", "Winner of Semifinal 2", PlayoffGameStatuses.Pending, null, null),
            new PlayoffGameResult(PlayoffRounds.ThirdPlace, 1, settings.ChampionshipWeek, null, null, null, null, "Loser of Semifinal 1", "Loser of Semifinal 2", PlayoffGameStatuses.Pending, null, null)
        ];
    }

    private static PlayoffGameResult CreateSeedGame(string round, int gameSlot, int week, PlayoffSeedResult? home, PlayoffSeedResult? away)
    {
        return new PlayoffGameResult(round, gameSlot, week, home?.Seed, away?.Seed, home?.AgentId, away?.AgentId, null, null, PlayoffGameStatuses.Pending, null, null);
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
