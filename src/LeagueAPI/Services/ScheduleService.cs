using LeagueAPI.Models;
using LeagueAPI.Data;
using Microsoft.EntityFrameworkCore;

namespace LeagueAPI.Services;

public sealed class ScheduleService(IDbContextFactory<LeagueApiDbContext> dbContextFactory, IAgentProfileReader agentProfileReader, LeagueStateService leagueStateService, MatchupScoringService matchupScoringService)
{
    private const long ScheduleGenerationLockKey = 55001;
    private const int RequiredTeamCount = 10;
    private const int RegularSeasonWeeks = 14;
    private const int SingleRoundRobinWeeks = RequiredTeamCount - 1;

    private readonly IDbContextFactory<LeagueApiDbContext> _dbContextFactory = dbContextFactory;
    private readonly IAgentProfileReader _agentProfileReader = agentProfileReader;
    private readonly LeagueStateService _leagueStateService = leagueStateService;
    private readonly MatchupScoringService _matchupScoringService = matchupScoringService;

    public async Task<GenerateScheduleResult> GenerateScheduleAsync(bool force, CancellationToken cancellationToken)
    {
        var teamIds = await GetParticipatingTeamIdsAsync(cancellationToken);

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        // Serialize concurrent generation so simultaneous calls cannot create duplicate schedules.
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({ScheduleGenerationLockKey})", cancellationToken);

        var existingMatchups = await dbContext.Matchups
            .OrderBy(matchup => matchup.Id)
            .ToListAsync(cancellationToken);

        if (existingMatchups.Count > 0 && !force)
        {
            await transaction.CommitAsync(cancellationToken);
            return new GenerateScheduleResult(
                false,
                "Schedule already generated. Skipping because force=false.",
                existingMatchups.Count);
        }

        if (existingMatchups.Count > 0)
            dbContext.Matchups.RemoveRange(existingMatchups);

        var leagueState = await _leagueStateService.GetLeagueStateAsync(cancellationToken);
        var generatedMatchups = BuildSchedule(teamIds, leagueState.Season);
        dbContext.Matchups.AddRange(generatedMatchups);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new GenerateScheduleResult(
            true,
            force && existingMatchups.Count > 0
                ? "Schedule regenerated."
                : "Schedule generated.",
            generatedMatchups.Count);
    }

    public async Task<IReadOnlyList<ScheduleMatchupResult>> GetScheduleAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var matchups = await dbContext.Matchups
            .AsNoTracking()
            .OrderBy(matchup => matchup.Week)
            .ThenBy(matchup => matchup.Id)
            .ToListAsync(cancellationToken);

        return await MapWithLiveScoresAsync(matchups, cancellationToken);
    }

    public async Task<IReadOnlyList<ScheduleMatchupResult>> GetScheduleForWeekAsync(int week, CancellationToken cancellationToken)
    {
        ValidateWeek(week);

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var matchups = await dbContext.Matchups
            .AsNoTracking()
            .Where(matchup => matchup.Week == week)
            .OrderBy(matchup => matchup.Id)
            .ToListAsync(cancellationToken);

        return await MapWithLiveScoresAsync(matchups, cancellationToken);
    }

    /// <summary>
    /// Maps matchups to results, filling in live starter totals for the current week's unfinished
    /// matchups. Finalized matchups keep the points stored when the week was finalized.
    /// </summary>
    private async Task<IReadOnlyList<ScheduleMatchupResult>> MapWithLiveScoresAsync(IReadOnlyList<MatchupEntity> matchups, CancellationToken cancellationToken)
    {
        var leagueState = await _leagueStateService.GetLeagueStateAsync(cancellationToken);
        var needsLiveScores = matchups.Any(matchup => !matchup.IsComplete && matchup.Week == leagueState.Week);
        if (!needsLiveScores)
            return matchups.Select(matchup => MapToScheduleMatchup(matchup, null)).ToList();

        var liveScoresByAgentId = await _matchupScoringService.GetLiveStarterScoresAsync(
            leagueState.Season,
            leagueState.Week,
            cancellationToken);

        return matchups
            .Select(matchup => MapToScheduleMatchup(
                matchup,
                !matchup.IsComplete && matchup.Week == leagueState.Week ? liveScoresByAgentId : null))
            .ToList();
    }

    public async Task<IReadOnlyList<AgentStanding>> GetStandingsAsync(CancellationToken cancellationToken)
    {
        var profiles = await _agentProfileReader.GetAgentProfilesAsync(true, cancellationToken);

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var completedMatchups = await dbContext.Matchups
            .AsNoTracking()
            .Where(matchup => matchup.IsComplete)
            .ToListAsync(cancellationToken);

        var standings = profiles
            .Select(profile =>
            {
                var agentMatchups = completedMatchups
                    .Where(matchup =>
                        string.Equals(matchup.HomeAgentId, profile.AgentId, StringComparison.Ordinal)
                        || string.Equals(matchup.AwayAgentId, profile.AgentId, StringComparison.Ordinal));
                var ties = agentMatchups.Count(matchup => matchup.IsTie);
                var wins = agentMatchups.Count(matchup =>
                    !matchup.IsTie
                    && string.Equals(matchup.WinnerAgentId, profile.AgentId, StringComparison.Ordinal));
                var losses = agentMatchups.Count() - wins - ties;

                return new AgentStanding(profile.AgentId, wins, losses, ties);
            })
            .OrderByDescending(standing => standing.Wins)
            .ThenBy(standing => standing.Losses)
            .ThenByDescending(standing => standing.Ties)
            .ThenBy(standing => standing.AgentId, StringComparer.Ordinal)
            .ToList();

        return standings;
    }

    public async Task<WeeklyMatchupResult?> GetMatchupForAgentAsync(string agentId, int week, CancellationToken cancellationToken)
    {
        var normalizedAgentId = NormalizeAgentId(agentId);
        ValidateWeek(week);

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var matchups = await dbContext.Matchups
            .AsNoTracking()
            .Where(matchup =>
                matchup.Week == week
                && (matchup.HomeAgentId == normalizedAgentId || matchup.AwayAgentId == normalizedAgentId))
            .OrderBy(matchup => matchup.Id)
            .Take(2)
            .ToListAsync(cancellationToken);

        if (matchups.Count == 0)
            return null;

        if (matchups.Count > 1)
            throw new InvalidOperationException($"Multiple matchups were found for agent '{normalizedAgentId}' in week {week}.");

        return MapToWeeklyMatchup(matchups[0], normalizedAgentId);
    }

    private async Task<IReadOnlyList<string>> GetParticipatingTeamIdsAsync(CancellationToken cancellationToken)
    {
        var profiles = await _agentProfileReader.GetAgentProfilesAsync(true, cancellationToken);
        var teamIds = profiles
            .Select(profile => profile.AgentId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(agentId => agentId, StringComparer.Ordinal)
            .ToArray();

        if (teamIds.Length != RequiredTeamCount)
            throw new InvalidOperationException($"Schedule generation requires exactly {RequiredTeamCount} enabled agent profiles, but found {teamIds.Length}.");

        return teamIds;
    }

    private static List<MatchupEntity> BuildSchedule(IReadOnlyList<string> teamIds, int season)
    {
        var rotation = teamIds.ToList();
        var matchups = new List<MatchupEntity>(RegularSeasonWeeks * (RequiredTeamCount / 2));
        var baseRounds = new List<List<(string HomeAgentId, string AwayAgentId)>>(SingleRoundRobinWeeks);

        for (var round = 0; round < SingleRoundRobinWeeks; round++)
        {
            var weeklyPairings = new List<(string HomeAgentId, string AwayAgentId)>(RequiredTeamCount / 2);

            for (var index = 0; index < rotation.Count / 2; index++)
            {
                var firstTeam = rotation[index];
                var secondTeam = rotation[rotation.Count - 1 - index];
                var homeFirst = (round + index) % 2 == 0;
                weeklyPairings.Add(homeFirst
                    ? (firstTeam, secondTeam)
                    : (secondTeam, firstTeam));
            }

            baseRounds.Add(weeklyPairings);
            AppendWeek(matchups, season, round + 1, weeklyPairings);
            RotateTeams(rotation);
        }

        for (var round = 0; round < RegularSeasonWeeks - SingleRoundRobinWeeks; round++)
        {
            var rematchPairings = baseRounds[round]
                .Select(pairing => (pairing.AwayAgentId, pairing.HomeAgentId))
                .ToList();

            AppendWeek(matchups, season, SingleRoundRobinWeeks + round + 1, rematchPairings);
        }

        return matchups;
    }

    private static void AppendWeek(List<MatchupEntity> matchups, int season, int week, IReadOnlyList<(string HomeAgentId, string AwayAgentId)> pairings)
    {
        foreach (var pairing in pairings)
        {
            matchups.Add(new MatchupEntity
            {
                Season = season,
                Week = week,
                MatchupType = MatchupTypes.RegularSeason,
                HomeAgentId = pairing.HomeAgentId,
                AwayAgentId = pairing.AwayAgentId,
                HomePoints = 0m,
                AwayPoints = 0m,
                IsComplete = false
            });
        }
    }

    private static void RotateTeams(List<string> rotation)
    {
        var lastTeam = rotation[^1];
        for (var index = rotation.Count - 1; index > 1; index--)
            rotation[index] = rotation[index - 1];

        rotation[1] = lastTeam;
    }

    private static ScheduleMatchupResult MapToScheduleMatchup(MatchupEntity matchup, IReadOnlyDictionary<string, decimal>? liveScoresByAgentId)
    {
        var homePoints = liveScoresByAgentId is null
            ? matchup.HomePoints
            : liveScoresByAgentId.GetValueOrDefault(matchup.HomeAgentId);
        var awayPoints = liveScoresByAgentId is null
            ? matchup.AwayPoints
            : liveScoresByAgentId.GetValueOrDefault(matchup.AwayAgentId);

        return new ScheduleMatchupResult(
            matchup.Id,
            matchup.Week,
            matchup.HomeAgentId,
            matchup.AwayAgentId,
            homePoints,
            awayPoints,
            matchup.IsComplete,
            matchup.WinnerAgentId,
            matchup.IsTie);
    }

    private static WeeklyMatchupResult MapToWeeklyMatchup(MatchupEntity matchup, string agentId)
    {
        var isHomeTeam = string.Equals(matchup.HomeAgentId, agentId, StringComparison.Ordinal);
        var opponentAgentId = isHomeTeam ? matchup.AwayAgentId : matchup.HomeAgentId;
        var normalizedOpponentAgentId = NormalizeOpponentAgentId(opponentAgentId, matchup.Week);

        return new WeeklyMatchupResult(
            matchup.Id,
            matchup.Week,
            agentId,
            normalizedOpponentAgentId,
            isHomeTeam,
            isHomeTeam ? matchup.HomePoints : matchup.AwayPoints,
            isHomeTeam ? matchup.AwayPoints : matchup.HomePoints,
            matchup.IsComplete);
    }

    private static string NormalizeAgentId(string agentId)
    {
        if (string.IsNullOrWhiteSpace(agentId))
            throw new ArgumentException("agentId is required.", nameof(agentId));

        return agentId.Trim();
    }

    private static string NormalizeOpponentAgentId(string? opponentAgentId, int week)
    {
        if (string.IsNullOrWhiteSpace(opponentAgentId))
            throw new InvalidOperationException($"Matchup for week {week} is missing an opponent agent ID.");

        var normalizedOpponentAgentId = opponentAgentId.Trim();
        if (string.Equals(normalizedOpponentAgentId, "BYE", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Matchup for week {week} contains an invalid BYE opponent.");

        return normalizedOpponentAgentId;
    }

    private static void ValidateWeek(int week)
    {
        if (week is < 1 or > 17)
            throw new ArgumentException("week must be between 1 and 17.", nameof(week));
    }
}
