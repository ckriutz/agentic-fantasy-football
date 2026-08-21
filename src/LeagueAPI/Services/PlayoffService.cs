using LeagueAPI.Data;
using LeagueAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace LeagueAPI.Services;

public sealed class PlayoffService(IDbContextFactory<LeagueApiDbContext> dbContextFactory, ScheduleService scheduleService)
{
    private readonly IDbContextFactory<LeagueApiDbContext> _dbContextFactory = dbContextFactory;
    private readonly ScheduleService _scheduleService = scheduleService;

    public async Task<PlayoffBracketResult> GetProjectedBracketAsync(int season, CancellationToken cancellationToken)
    {
        if (season <= 0)
            throw new ArgumentException("season must be a positive integer.", nameof(season));

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var settings = await dbContext.PlayoffSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(settings => settings.Id == PlayoffSettingsDefaults.SingletonId, cancellationToken)
            ?? new PlayoffSettingsEntity();

        ValidateSupportedSettings(settings);

        var standings = await _scheduleService.GetStandingsAsync(season, cancellationToken);
        var completedMatchups = await dbContext.Matchups
            .AsNoTracking()
            .Where(matchup =>
                matchup.Season == season
                && matchup.MatchupType == MatchupTypes.RegularSeason
                && matchup.IsComplete)
            .ToListAsync(cancellationToken);

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
