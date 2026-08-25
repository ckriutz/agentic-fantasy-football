using LeagueAPI.Data;
using LeagueAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace LeagueAPI.Services;

/// <summary>
/// Derives playoff eligibility (active vs eliminated) for every agent in a season from the
/// locked bracket, persisted seeds, bracket games, and their linked matchups. No mutable
/// elimination state is stored: the persisted playoff facts are the single source of truth,
/// so this read model can never drift out of sync with round advancement.
/// </summary>
public sealed class PlayoffEliminationService(IDbContextFactory<LeagueApiDbContext> dbContextFactory, IAgentProfileReader agentProfileReader)
{
    private readonly IDbContextFactory<LeagueApiDbContext> _dbContextFactory = dbContextFactory;
    private readonly IAgentProfileReader _agentProfileReader = agentProfileReader;

    public async Task<PlayoffEligibilityResult> GetEligibilityAsync(int season, CancellationToken cancellationToken)
    {
        ValidateSeason(season);

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var bracket = await dbContext.PlayoffBrackets.AsNoTracking().FirstOrDefaultAsync(row => row.Season == season, cancellationToken);
        if (bracket is null || bracket.Status == PlayoffBracketStatuses.Projected)
            return await BuildPreLockResultAsync(dbContext, season, cancellationToken);

        var settings = await GetSettingsAsync(dbContext, cancellationToken);
        var seedEntities = await dbContext.PlayoffSeeds.AsNoTracking().Where(seed => seed.BracketId == bracket.Id).OrderBy(seed => seed.Seed).ToListAsync(cancellationToken);
        var gameEntities = await dbContext.PlayoffBracketGames.AsNoTracking().Where(game => game.BracketId == bracket.Id).OrderBy(game => game.Round).ThenBy(game => game.GameSlot).ToListAsync(cancellationToken);
        var matchupIds = gameEntities.Where(game => game.MatchupId.HasValue).Select(game => game.MatchupId!.Value).ToList();
        var matchups = await dbContext.Matchups.AsNoTracking().Where(matchup => matchupIds.Contains(matchup.Id)).ToDictionaryAsync(matchup => matchup.Id, cancellationToken);

        return BuildResult(season, bracket.Status, settings, seedEntities, gameEntities, matchups, await LoadSeasonParticipantIdsAsync(dbContext, season, cancellationToken));
    }

    public async Task<PlayoffAgentStatusResult?> GetAgentStatusAsync(int season, string agentId, CancellationToken cancellationToken)
    {
        ValidateSeason(season);
        ValidateAgentId(agentId);

        var eligibility = await GetEligibilityAsync(season, cancellationToken);
        return eligibility.Agents.FirstOrDefault(agent => string.Equals(agent.AgentId, agentId.Trim(), StringComparison.Ordinal));
    }

    /// <summary>Reusable enforcement hook for SeasonRunner/waiver filtering. Unknown agents stay active so regular-season behavior is unchanged.</summary>
    public async Task<bool> IsAgentActiveAsync(int season, string agentId, CancellationToken cancellationToken)
    {
        var status = await GetAgentStatusAsync(season, agentId, cancellationToken);
        return status is null || string.Equals(status.Status, PlayoffParticipantStatuses.Active, StringComparison.Ordinal);
    }

    private async Task<IReadOnlyList<string>> LoadSeasonParticipantIdsAsync(LeagueApiDbContext dbContext, int season, CancellationToken cancellationToken)
    {
        // The participant universe is frozen from the season's persisted schedule facts so later
        // profile enable/disable changes cannot rewrite who belonged to that season.
        var homeAgentIds = dbContext.Matchups.AsNoTracking().Where(matchup => matchup.Season == season).Select(matchup => matchup.HomeAgentId);
        var awayAgentIds = dbContext.Matchups.AsNoTracking().Where(matchup => matchup.Season == season).Select(matchup => matchup.AwayAgentId);
        var agentIds = await homeAgentIds.Concat(awayAgentIds).Distinct().ToListAsync(cancellationToken);
        agentIds.Sort(StringComparer.Ordinal);
        return agentIds;
    }

    private async Task<PlayoffEligibilityResult> BuildPreLockResultAsync(LeagueApiDbContext dbContext, int season, CancellationToken cancellationToken)
    {
        var settings = await GetSettingsAsync(dbContext, cancellationToken);
        var profiles = await _agentProfileReader.GetAgentProfilesAsync(enabledOnly: true, cancellationToken);
        var allAgentIds = profiles.Select(profile => profile.AgentId).Distinct(StringComparer.Ordinal).OrderBy(agentId => agentId, StringComparer.Ordinal).ToList();
        var agents = allAgentIds.Select(agentId => new PlayoffAgentStatusResult(agentId, PlayoffParticipantStatuses.Active, PlayoffEliminationReasons.RegularSeason, null, false, null, null)).ToList();
        return new PlayoffEligibilityResult(season, false, null, settings.RegularSeasonEndWeek, settings.PlayoffStartWeek, settings.ChampionshipWeek, settings.ThirdPlaceGameEnabled, allAgentIds.ToList(), [], agents);
    }

    private static PlayoffEligibilityResult BuildResult(int season, string bracketStatus, PlayoffSettingsEntity settings, IReadOnlyList<PlayoffSeedEntity> seedEntities, IReadOnlyList<PlayoffBracketGameEntity> gameEntities, IReadOnlyDictionary<int, MatchupEntity> matchups, IReadOnlyList<string> allAgentIds)
    {
        var statusesByAgentId = new Dictionary<string, AgentStatus>(StringComparer.Ordinal);
        foreach (var seed in seedEntities)
            statusesByAgentId[seed.AgentId] = new AgentStatus(seed.Seed);

        // Non-playoff teams are eliminated the moment the bracket locks; they never appear in seeds.
        foreach (var agentId in allAgentIds.Where(agentId => !statusesByAgentId.ContainsKey(agentId)))
            statusesByAgentId[agentId] = new AgentStatus(null, PlayoffEliminationReasons.NotInPlayoffs);

        var seedsByAgentId = seedEntities.ToDictionary(seed => seed.AgentId, seed => seed.Seed);
        var resolvedGames = gameEntities.Select(game => ResolvedGame.From(game, matchups, seedsByAgentId)).Where(game => game is not null).Select(game => game!).OrderBy(game => Array.IndexOf(PlayoffRounds.All, game.Round)).ThenBy(game => game.GameSlot).ToList();

        ApplyWildCardResults(statusesByAgentId, FilterGames(resolvedGames, PlayoffRounds.WildCard));
        ApplySemifinalResults(statusesByAgentId, FilterGames(resolvedGames, PlayoffRounds.Semifinal), settings.ThirdPlaceGameEnabled);
        ApplyFinalWeekResults(statusesByAgentId, FilterGames(resolvedGames, PlayoffRounds.Championship), FilterGames(resolvedGames, PlayoffRounds.ThirdPlace), settings.ThirdPlaceGameEnabled);
        if (bracketStatus == PlayoffBracketStatuses.Complete)
        {
            foreach (var status in statusesByAgentId.Values.Where(status => status.Status == PlayoffParticipantStatuses.Active))
                status.Deactivate();
        }

        var orderedAgents = statusesByAgentId.OrderBy(pair => pair.Value.Seed.HasValue ? 0 : 1).ThenBy(pair => pair.Value.Seed ?? int.MaxValue).ThenBy(pair => pair.Key, StringComparer.Ordinal).ToList();
        var activeAgentIds = orderedAgents.Where(pair => pair.Value.Status == PlayoffParticipantStatuses.Active).Select(pair => pair.Key).ToList();
        var eliminatedAgentIds = orderedAgents.Where(pair => pair.Value.Status == PlayoffParticipantStatuses.Eliminated).Select(pair => pair.Key).ToList();
        var agents = orderedAgents.Select(pair => pair.Value.ToResult(pair.Key)).ToList();

        return new PlayoffEligibilityResult(season, true, bracketStatus, settings.RegularSeasonEndWeek, settings.PlayoffStartWeek, settings.ChampionshipWeek, settings.ThirdPlaceGameEnabled, activeAgentIds, eliminatedAgentIds, agents);
    }

    private static IReadOnlyList<ResolvedGame> FilterGames(IReadOnlyList<ResolvedGame> resolvedGames, string round) => resolvedGames.Where(game => string.Equals(game.Round, round, StringComparison.Ordinal)).ToList();

    private static void ApplyWildCardResults(Dictionary<string, AgentStatus> statusesByAgentId, IReadOnlyList<ResolvedGame> wildCardGames)
    {
        foreach (var game in wildCardGames)
            EliminateLoser(statusesByAgentId, game, PlayoffEliminationReasons.WildCardLoss, PlayoffRounds.WildCard);
    }

    private static void ApplySemifinalResults(Dictionary<string, AgentStatus> statusesByAgentId, IReadOnlyList<ResolvedGame> semifinalGames, bool thirdPlaceGameEnabled)
    {
        if (thirdPlaceGameEnabled)
            return;

        // With no third-place game, a semifinal loss ends the season immediately; otherwise the loser stays active through week 17.
        foreach (var game in semifinalGames)
            EliminateLoser(statusesByAgentId, game, PlayoffEliminationReasons.SemifinalLoss, PlayoffRounds.Semifinal);
    }

    private static void ApplyFinalWeekResults(Dictionary<string, AgentStatus> statusesByAgentId, IReadOnlyList<ResolvedGame> championshipGames, IReadOnlyList<ResolvedGame> thirdPlaceGames, bool thirdPlaceGameEnabled)
    {
        if (thirdPlaceGameEnabled)
        {
            foreach (var game in thirdPlaceGames)
                EliminateLoser(statusesByAgentId, game, PlayoffEliminationReasons.ThirdPlaceLoss, PlayoffRounds.ThirdPlace);
            foreach (var game in championshipGames)
                EliminateLoser(statusesByAgentId, game, PlayoffEliminationReasons.ChampionshipLoss, PlayoffRounds.Championship);
            return;
        }

        // Without a third-place game both semifinal losers are done once the championship completes.
        if (championshipGames.All(game => game.IsComplete))
        {
            foreach (var game in championshipGames)
                EliminateLoser(statusesByAgentId, game, PlayoffEliminationReasons.SemifinalLoss, PlayoffRounds.Championship);
        }
    }

    private static void EliminateLoser(Dictionary<string, AgentStatus> statusesByAgentId, ResolvedGame game, string reason, string eliminatedRound)
    {
        if (game.LoserAgentId is null || !statusesByAgentId.TryGetValue(game.LoserAgentId, out var loser))
            return;

        loser.Eliminate(reason, eliminatedRound, game.Week);
    }

    private static async Task<PlayoffSettingsEntity> GetSettingsAsync(LeagueApiDbContext dbContext, CancellationToken cancellationToken)
    {
        return await dbContext.PlayoffSettings.AsNoTracking().FirstOrDefaultAsync(settings => settings.Id == PlayoffSettingsDefaults.SingletonId, cancellationToken)
            ?? new PlayoffSettingsEntity();
    }

    private static void ValidateSeason(int season)
    {
        if (season <= 0)
            throw new ArgumentException("season must be a positive integer.", nameof(season));
    }

    private static void ValidateAgentId(string agentId)
    {
        if (string.IsNullOrWhiteSpace(agentId))
            throw new ArgumentException("agentId is required.", nameof(agentId));
    }

    private sealed class AgentStatus(int? seed, string reason = PlayoffEliminationReasons.InContention)
    {
        public int? Seed { get; } = seed;

        public string Status { get; private set; } = PlayoffParticipantStatuses.Active;

        public string Reason { get; private set; } = reason;

        public string? EliminatedRound { get; private set; }

        public int? EliminatedWeek { get; private set; }

        public void Eliminate(string reason, string round, int week)
        {
            Status = PlayoffParticipantStatuses.Eliminated;
            Reason = reason;
            EliminatedRound = round;
            EliminatedWeek = week;
        }

        public void Deactivate()
        {
            Status = PlayoffParticipantStatuses.Inactive;
            Reason = PlayoffEliminationReasons.SeasonComplete;
        }

        public PlayoffAgentStatusResult ToResult(string agentId) => new(agentId, Status, Reason, Seed, Seed.HasValue, EliminatedRound, EliminatedWeek);
    }

    /// <summary>A bracket game whose participants and outcome are fully resolved from its linked matchup.</summary>
    private sealed class ResolvedGame(string round, int gameSlot, int week, bool isComplete, string? winnerAgentId, string? loserAgentId)
    {
        public string Round { get; } = round;

        public int GameSlot { get; } = gameSlot;

        public int Week { get; } = week;

        public bool IsComplete { get; } = isComplete;

        public string? WinnerAgentId { get; } = winnerAgentId;

        public string? LoserAgentId { get; } = loserAgentId;

        public static ResolvedGame? From(PlayoffBracketGameEntity game, IReadOnlyDictionary<int, MatchupEntity> matchups, Dictionary<string, int> seedsByAgentId)
        {
            if (!game.MatchupId.HasValue || !matchups.TryGetValue(game.MatchupId.Value, out var matchup))
                return null;
            if (!string.Equals(game.Status, PlayoffGameStatuses.Complete, StringComparison.Ordinal) || !matchup.IsComplete)
                return null;

            var winnerAgentId = ResolveWinnerAgentId(matchup, seedsByAgentId);
            var loserAgentId = winnerAgentId is null ? null : string.Equals(winnerAgentId, matchup.HomeAgentId, StringComparison.Ordinal) ? matchup.AwayAgentId : matchup.HomeAgentId;
            return new ResolvedGame(game.Round, game.GameSlot, game.Week, winnerAgentId is not null, winnerAgentId, loserAgentId);
        }

        private static string? ResolveWinnerAgentId(MatchupEntity matchup, Dictionary<string, int> seedsByAgentId)
        {
            if (!matchup.IsComplete)
                return null;
            if (matchup.IsTie || matchup.WinnerAgentId is null)
            {
                // League policy: a tied completed playoff matchup goes to the higher seed.
                var homeSeed = SeedForAgent(matchup.HomeAgentId, seedsByAgentId);
                var awaySeed = SeedForAgent(matchup.AwayAgentId, seedsByAgentId);
                return homeSeed <= awaySeed ? matchup.HomeAgentId : matchup.AwayAgentId;
            }
            return matchup.WinnerAgentId;
        }

        private static int SeedForAgent(string? agentId, Dictionary<string, int> seedsByAgentId) => agentId is not null && seedsByAgentId.TryGetValue(agentId, out var seed) ? seed : int.MaxValue;
    }
}
