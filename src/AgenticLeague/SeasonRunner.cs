using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

public class SeasonRunner
{
    private static readonly TimeZoneInfo EasternTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
    private readonly List<FantasyAgent> _agents;
    private readonly ILogger _logger;
    private readonly HttpClient _http;
    private readonly HttpClient _scoresHttp;
    private LeagueState _leagueState;

    public SeasonRunner(List<FantasyAgent> agents, ILogger logger, HttpClient http, HttpClient scoresHttp)
    {
        _agents = agents;
        _logger = logger;
        _http = http;
        _scoresHttp = scoresHttp;
        _leagueState = LeagueStateHelper.GetLeagueStateAsync(_http, _logger).Result;
    }

    // In case for testing I wanted to pass in a custom league state, I can use this constructor instead of the one above.
    public SeasonRunner(List<FantasyAgent> agents, ILogger logger, HttpClient http, HttpClient scoresHttp, LeagueState leagueState)
    {
        _agents = agents;
        _logger = logger;
        _http = http;
        _scoresHttp = scoresHttp;
        _leagueState = leagueState;
    }

    public async Task RunAsync()
    {
        _logger.LogInformation("Season runner is starting.");
        if (_leagueState is null)
        {
            // This shouldn't be the case, but whatever.
            _logger.LogError("Season runner cannot continue because league state is unavailable.");
            return;
        }
        if (_leagueState.Week <= 0)
        {
            _logger.LogWarning("Season runner cannot continue because the league is in preseason or week 0.");
            return;
        }

        // All league-day decisions use US Eastern Time, including daylight-saving transitions.
        var easternNow = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, EasternTimeZone);
        //var dayOfWeek = easternNow.DayOfWeek;
        var dayOfWeek = DayOfWeek.Sunday; // TESTING. Change back when done testing.
        _logger.LogInformation("Today is {DayOfWeek} in US Eastern Time ({EasternNow}).", dayOfWeek, easternNow);

        if(dayOfWeek == DayOfWeek.Tuesday)
        {
            _logger.LogInformation("Running Tuesday tasks.");

            // Tuesday!! This is really the first day of the new week, now that Monday night has passed.
            // Here is where we will first see how the week went and update wins/losses and any other stats.
            // Then prompt the agents to reflect on how they did for the previous week and plan for the upcoming week.
            // Lastly, we will prompt the agents to make any waiver claims they want to for the new week, since the waiver wire opens on Tuesday.
            // I only plan on running this once this week.

            // This is the check to ensure that we only proceed with the Tuesday rollover if the league is in the correct phase.
            if (!string.Equals(_leagueState.Phase, "games_locked", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Skipping Tuesday rollover for season {Season} week {Week} because the league phase is {Phase}, not games_locked.", _leagueState.Season, _leagueState.Week, _leagueState.Phase);
                return;
            }

            // First, we process weekly scores for the current week to update matchups.
            var weeklyScoresSuccess = await ProcessWeeklyScoresAsync(_leagueState.Season, _leagueState.Week, forceRefresh: true);
            if (!weeklyScoresSuccess)
            {
                _logger.LogError($"Weekly scores sync failed for season {_leagueState.Season}, week {_leagueState.Week}; league state will not advance.");
                return;
            }

            // Then, we finalize the matchups for the current week to ensure all results are recorded.
            // This "locks in" the results for the week, ensuring that the matchups are finalized and no further changes can be made to the scores or outcomes.
            var finalized = await FinalizeMatchupsForWeekAsync(_leagueState.Season, _leagueState.Week);
            if (!finalized)
            {
                _logger.LogError($"Matchup finalization failed for season {_leagueState.Season}, week {_leagueState.Week}; league state will not advance.");
                return;
            }
            _logger.LogInformation($"✅ Matchups finalized for season {_leagueState.Season}, week {_leagueState.Week}.");

            // Then we will advance the league state to the next week, and set the phase to waiver-wire, so the agents can make any waiver claims they want to for the new week.
            int completedWeek = _leagueState.Week;
            int week = completedWeek + 1;
            _leagueState = await LeagueStateHelper.SetLeagueStateAsync(_leagueState.Season, week, "waiver_window", "season-runner", _http, _logger);

            foreach(var agent in _agents)
            {
                var reflectionPrompt =
                $"""
                You are {agent.GetAgentName()}. Season {_leagueState.Season} Week {completedWeek} is finalized.
                Use the `weekly-reflection` tool and review exactly Week {completedWeek}; the current league week is {week}, but do not query it as the completed matchup.

                Required calls: `ReadAgentBootstrap({agent.GetAgentName()})`,
                `GetWeeklyMatchup({agent.GetAgentName()}, {completedWeek})`, `GetMyRoster({agent.GetAgentName()})`, then `WriteAgentBootstrap` once.

                Do not use `SearchWeb` for this task. Do not use any other tools. Do not make any roster or lineup changes. Do not make any waiver claims. Do not make any trades. Do not make any draft picks. Do not make any other moves.
                """;
                var response = (await agent.RunAsync(reflectionPrompt)).Response;
                await DecisionLogger.LogDecisionAsync(agent.GetAgentName()!, completedWeek, "Weekly Reflection", response, $"Weekly Reflection for week {completedWeek}", _logger);
            }

            // Then we will prompt the agents to evaluate whether they should make waiver claims.
            foreach(var agent in _agents)
            {
                var prompt =
                $"""
                You are {agent.GetAgentName()}. Today is Tuesday, a brand-new week in season {_leagueState.Season} week {week}.
                Use the `weekly-player-management` skill to evaluate whether meaningful waiver claims improve your roster.
                Submit waiver claims only when they are justified; a well-supported no-move outcome is valid.
                Do not end with a tool call or plan. Return the required decision summary as visible text, even when no claims are submitted.
                """;
                var response = (await agent.RunAsync(prompt)).Response;
                await DecisionLogger.LogDecisionAsync(agent.GetAgentName()!, week, "Roster Management", response, "Waiver Claim Attempt", _logger);
            }

            // No reason on this day, to prompt the agents to set their lineups, since the games don't start until Thursday, and they might want to make waiver claims before setting their lineups.
        }
        if(dayOfWeek == DayOfWeek.Wednesday)
        {
            _logger.LogInformation("Running Wednesday tasks.");

            // Wednesday! This is the day after the waiver wire opened.
            // Here we will process the waiver claims, and let the agents know which claims were successful and which were not, and update their rosters accordingly.
            // First, lets check to see if the status is still on waiver-wire, if it is, then we will process the waiver claims, and then update the league state to free-agency.
            // Planning on running this twice a day, first in the morning, an then again in the afternoon.
            if (_leagueState?.Phase == "waiver_window")
            {
                bool success = await ProcessWaiverClaimsAsync(_leagueState.Season, _leagueState.Week);
                if (success)
                {
                    // Now, lets ONLY prompt the agents who submitted waiver claims to let them know which claims were successful and which were not, and update their rosters accordingly.
                    var waiverResults = await GetWaiverClaimResults(_leagueState.Season, _leagueState.Week);
                    if (waiverResults is null)
                    {
                        _logger.LogError("Failed to retrieve waiver claim results for season {Season} week {Week}.", _leagueState.Season, _leagueState.Week);
                        return;
                    }

                    foreach (var group in waiverResults.GroupBy(claim => claim.AgentId).ToList())
                    {
                        var agent = _agents.FirstOrDefault(a => a.GetAgentName() == group.Key);
                        if (agent is null) { continue; }

                        var payload = new
                        {
                            agentId = group.Key,
                            season = _leagueState.Season,
                            week = _leagueState.Week,
                            phase = "free_agency",
                            waiversProcessedAtUtc = group.Max(claim => claim.ProcessedAtUtc),
                            claims = group
                        };

                        var prompt =
                        $"""
                        Today is Wednesday, and all waiver wire claims have been processed for season {_leagueState.Season} week {_leagueState.Week}.
                        You are {agent.GetAgentName()}. Use this exact agent ID for every tool call.
                        Use the `waiver-result-review` skill to review your waiver outcomes.
                        Only if the review confirms a successful claim and the roster confirms that it could improve a fillable lineup slot, use the `roster-management` skill to update your lineup.
                        Do not end with a tool call or plan. Return the required decision summary as visible text, even when no changes are made.

                        Here are your waiver claim results for season {_leagueState.Season} week {_leagueState.Week}:
                        {JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true })}
                        """;

                        var response = (await agent.RunAsync(prompt)).Response;
                        
                        await DecisionLogger.LogDecisionAsync(agent.GetAgentName()!, _leagueState.Week, "Roster Management", response, "Update Roster post Waiver", _logger);
                    }

                    
                    // Then we will set the league state to free-agency, and the agents can start making roster moves outside of the waiver wire if they want to.
                    _leagueState = await LeagueStateHelper.SetLeagueStateAsync(_leagueState.Season, _leagueState.Week, "free_agency", "season-runner", _http, _logger);
                }
                else
                {
                    _logger.LogWarning("Failed to process waiver claims for season {Season} week {Week}.", _leagueState.Season, _leagueState.Week);
                }
                return;
            }
            if (_leagueState?.Phase == "free_agency")
            {
                foreach(var agent in _agents)
                {
                    var agentId = agent.GetAgentName()!;
                    var prompt =
                    $"""
                    Today is Wednesday of season {_leagueState.Season}, week {_leagueState.Week}, and the league is in free agency.
                    You are {agentId}. Use this exact agent ID for every tool call.
                    Use only the `weekly-player-management` skill to decide whether one meaningful free-agent add/drop would improve your roster.
                    A no-change decision is valid. Do not modify agent profiles or bootstrap status.
                    Return the skill's required decision summary as visible text, even when no change is made.
                    """;
                    var response = (await agent.RunAsync(prompt)).Response;
                    await DecisionLogger.LogDecisionAsync(agentId, _leagueState.Week, "Roster Management", response, "Update Roster", _logger);
                }
            }

        }
        if(dayOfWeek == DayOfWeek.Thursday)
        {
            _logger.LogInformation("Running Thursday tasks.");

            // Thursday! This is the day when some games start, so we want to make sure the agents have set their lineups correctly for the games that are happening today.
            // Also, later at night, maybe we run this again to get some scores, as well as lock the players who have played.
            
            // TODO: Turn this back on once we're ready to process weekly scores for Thursday games.
            //var weeklyScoresSuccess = await ProcessWeeklyScoresAsync(_leagueState.Season, _leagueState.Week);
            //            if (!weeklyScoresSuccess)
            //            {
            //                _logger.LogError($"Weekly scores sync failed for season {_leagueState.Season}, week {_leagueState.Week}; league state will not advance.");
            //                return;
            //            }

            // Still in free-agency, so agents can still make roster moves if they want to.
            // Some games start on Thursday, so we will want to make sure the agents have set their lineups before the games start.
            // We will want to do this before games start on Thursday.
            if (_leagueState?.Phase == "free_agency")
            {
                foreach(var agent in _agents)
                {
                    var prompt =
                    $"""
                    Today is Thursday. We are in season {_leagueState.Season} week {_leagueState.Week}.
                    You are {agent.GetAgentName()}. Use this exact agent ID for every tool call.
                    We are still in the free-agency phase, however some players may have games today.
                    This is your chance to make sure these players are set correctly for their games today.
                    Remember, all your starting roster slots must be filled.
                    Use the `weekly-player-management` skill to evaluate whether a meaningful free-agent add/drop improves your roster.
                    When you're done, use the `roster-management` skill to update your lineup, if necessary.
                    Return the skill's required decision summary as visible text, even when no change is made.
                    """;

                    var response = (await agent.RunAsync(prompt)).Response;
                    await DecisionLogger.LogDecisionAsync(agent.GetAgentName()!, _leagueState.Week, "Roster Management", response, "Update Roster", _logger);
                }
            }

        }
        if(dayOfWeek == DayOfWeek.Friday)
        {
            _logger.LogInformation("Running Friday tasks.");

            // Friday! This is the day after the Thursday games, so we want to make sure the agents have set their lineups correctly for the games that are happening on Sunday and Monday.
            // Nothing else really happens today.
            
            // TODO: Turn this back on! Lets process weekly scores for the Thursday games.
            //var weeklyScoresSuccess = await ProcessWeeklyScoresAsync(_leagueState.Season, _leagueState.Week);
            //if (!weeklyScoresSuccess)
            //{
                //_logger.LogError($"Weekly scores sync failed for season {_leagueState.Season}, week {_leagueState.Week}; league state will not advance.");
                //return;
            //}

            // Still in free-agency, so agents can make any last minute roster moves before the games start on Sunday and Monday.
            // Players who are injured or questionable for the weekend games might get dropped on Friday, so this is the last chance for agents to pick them up before the games start.
            // Players who played on Thursday are stuck where they are.
            if (_leagueState?.Phase == "free_agency")
            {
                foreach(var agent in _agents)
                {
                    var prompt =
                    $"""
                    Today is Friday. We are in season {_leagueState.Season} week {_leagueState.Week}.
                    You are {agent.GetAgentName()}. Use this exact agent ID for every tool call.
                    We are still in the free-agency phase, and while there are no games today, this is a quick oppertunity to make sure your roster is set correctly for the upcoming games on Sunday and Monday.
                    Remember, all your starting roster slots must be filled.
                    Use the `weekly-player-management` skill to evaluate whether a meaningful free-agent add/drop improves your roster.
                    When you're done, use the `roster-management` skill to update your lineup, if necessary.
                    Return the skill's required decision summary as visible text, even when no change is made.
                    """;

                    var response = (await agent.RunAsync(prompt)).Response;
                    await DecisionLogger.LogDecisionAsync(agent.GetAgentName()!, _leagueState.Week, "Roster Management", response, "Update Roster", _logger);
                }
            }
        }
        if(dayOfWeek == DayOfWeek.Saturday)
        {
            _logger.LogInformation("Running Saturday tasks.");

            // It's Saturday! This is the day before the Sunday games, so agents have one last chance to make roster moves before the games start.
            // Still in free-agency, so agents can make any last minute roster moves before the games start on Sunday and Monday.
            // Players who are injured or questionable for the weekend games might get dropped on Friday, so this is the last chance for agents to pick them up before the games start.
            // Players who played on Thursday are stuck where they are.

            // TODO: Turn this back on. Why not do a sync.
            //bool weeklyScoresSuccess = await ProcessWeeklyScoresAsync(_leagueState?.Season ?? 0, _leagueState?.Week ?? 0);
            //if (weeklyScoresSuccess)
            //{
                //_logger.LogInformation("Successfully processed weekly scores for the week.");
            //}

            if (_leagueState?.Phase == "free_agency")
            {
                foreach(var agent in _agents)
                {
                    var prompt =
                    $"""
                    Today is Saturday. We are in season {_leagueState.Season} week {_leagueState.Week}.
                    Your goal is to ensure you have the best possible roster for the upcoming games on Sunday and Monday. Do not worry about next week.
                    You are {agent.GetAgentName()}. Use this exact agent ID for every tool call.
                    We are still in the free-agency phase, however some players may have had games on Thursday, and if so, they will be locked in their current roster spots.
                    This is one of your last chances to make any roster moves before the games start on Sunday and Monday.
                    Use the `weekly-player-management` skill to evaluate whether a meaningful free-agent add/drop improves your roster.
                    When you're done, use the `roster-management` skill to update your lineup.
                    """;
                    var response = (await agent.RunAsync(prompt)).Response;
                    await DecisionLogger.LogDecisionAsync(agent.GetAgentName()!, _leagueState.Week, "Roster Management", response, "Update Roster", _logger);
                }
            }
        }
        if(dayOfWeek == DayOfWeek.Sunday)
        {
            _logger.LogInformation("Running Sunday tasks.");

            // It's Sunday! Games are starting today, so lets do one final oppertunity for the agents to set their lineups for the Sunday games before they start.
            // TODO: Turn this back on once weekly scores processing is needed.
            //bool weeklyScoresSuccess = await ProcessWeeklyScoresAsync(_leagueState?.Season ?? 0, _leagueState?.Week ?? 0);
            //if (weeklyScoresSuccess)
            //{
                //_logger.LogInformation("Successfully processed weekly scores for the week.");
            //}

            // Only lineup changes are allowed on Sunday, so agents can only set their lineups for the Sunday games, but they can't make any roster moves.
            // Lets set the league state to games_locked, since the games start on Sunday, and we want to make sure the agents have set their lineups before the games start.
            _leagueState = await LeagueStateHelper.SetLeagueStateAsync(_leagueState?.Season, _leagueState?.Week, "games_locked", "season-runner", _http, _logger);


            foreach(var agent in _agents)
            {
                var prompt =
                $"""
                Today is Sunday. We are in season {_leagueState.Season} week {_leagueState.Week}.
                You are {agent.GetAgentName()}. Use this exact agent ID for every tool call.
                There are games starting today, so this is your last chance to set your lineups for the Sunday games.
                Preserve every player whose `lockStatus.isLineupMoveLocked` is true. Optimize only players who have not played yet.
                This is a lineup-only run. Do not add, drop, or claim players.
                Use the `roster-management` skill to update your lineup. Do not load or use any other skill in this run.
                """;
                var response = (await agent.RunAsync(prompt)).Response;
                await DecisionLogger.LogDecisionAsync(agent.GetAgentName()!, _leagueState.Week, "Roster Management", response, "Update Roster", _logger);
            }

        }
        if(dayOfWeek == DayOfWeek.Monday)
        {
            // Only lineup changes are allowed on Monday, so agents can only set their lineups for the Monday game, but they can't make any roster moves.
            // We will want to make sure the agents have set their lineups before the game starts on Monday.
            // TODO: Wire in a prompt to remind the agents to set their lineups for the Monday game if they haven't already, since the last game starts on Monday night.
            _logger.LogInformation("Running Monday tasks.");

            bool weeklyScoresSuccess = await ProcessWeeklyScoresAsync(_leagueState?.Season ?? 0, _leagueState?.Week ?? 0);
            if (weeklyScoresSuccess)
            {
                _logger.LogInformation("Successfully processed weekly scores for the week.");
            }
            else
            {
                _logger.LogError($"Weekly scores sync failed for season {_leagueState.Season}, week {_leagueState.Week}; league state will not advance.");
                return;
            }

            
            foreach(var agent in _agents)
            {
                var prompt =
                $"""
                Today is Monday of season {_leagueState.Season}, Week {_leagueState.Week}.
                You are {agent.GetAgentName()}; use this exact ID for every tool call.
                It's likely most, if not all your players have played, but if any of your players have not played yet, you can still set your lineup for the Monday game.
                Only compare and move players who have not played and are still unlocked.
                Locked starters will not be able to be moved, and locked bench players will not be able to be moved.
                Use the `roster-management` skill to update your lineup. Do not load or use any other skill in this run.
                """;
                var response = (await agent.RunAsync(prompt)).Response;
                await DecisionLogger.LogDecisionAsync(agent.GetAgentName()!, _leagueState.Week, "Roster Management", response, "Update Roster", _logger);
            }
        }
    }

    private async Task<bool> ProcessWaiverClaimsAsync(int season, int week)
    {
        // This is where we will process the waiver claims.
        var waiverResponse = await _http.PostAsync($"api/league/waivers/{season}/{week}/process", null);
        if (!waiverResponse.IsSuccessStatusCode)
        {
            var error = await waiverResponse.Content.ReadAsStringAsync();
            _logger.LogError("Failed to process waiver claims for Season: {Season}, Week: {Week}. Status code: {StatusCode}. Response: {Response}", season, week, waiverResponse.StatusCode, error);
            return false;
        }

        _logger.LogInformation("Successfully processed waiver claims for Season: {Season}, Week: {Week}.", season, week);
        return true;
    }

    private async Task<bool> ProcessWeeklyScoresAsync(int season, int week, bool forceRefresh = false)
    {
        if (!forceRefresh)
        {
            var latestSyncResponse = await _http.GetAsync($"api/sync/fantasypros/points/latest?season={season}");
            if (latestSyncResponse.IsSuccessStatusCode)
            {
                var latestSyncJson = await latestSyncResponse.Content.ReadAsStringAsync();
                var latestSync = JsonSerializer.Deserialize<FantasyProsPointsSyncStatus>(latestSyncJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (latestSync is null)
                {
                    _logger.LogError("FantasyPros points status for season {Season}, week {Week} could not be parsed.", season, week);
                    return false;
                }

                if (string.Equals(latestSync.Status, "Succeeded", StringComparison.OrdinalIgnoreCase)
                    && latestSync.RecordCount is > 0
                    && latestSync.Season == season
                    && latestSync.EndWeek >= week
                    && latestSync.CompletedAtUtc.HasValue
                    && DateTimeOffset.UtcNow - latestSync.CompletedAtUtc.Value < TimeSpan.FromHours(2))
                {
                    _logger.LogInformation("Skipping FantasyPros points sync for season {Season}, week {Week}; the last successful weekly scores sync completed at {CompletedAtUtc}.", season, week, latestSync.CompletedAtUtc.Value);
                    return true;
                }
            }
            else if (latestSyncResponse.StatusCode != System.Net.HttpStatusCode.NotFound)
            {
                var error = await latestSyncResponse.Content.ReadAsStringAsync();
                _logger.LogError("Failed to load FantasyPros points status for season {Season}, week {Week}. Status code: {StatusCode}. Response: {Response}", season, week, latestSyncResponse.StatusCode, error);
                return false;
            }
        }

        var scoresResponse = await _scoresHttp.PostAsync("api/sync/fantasypros/points", null);
        if (!scoresResponse.IsSuccessStatusCode)
        {
            var error = await scoresResponse.Content.ReadAsStringAsync();
            _logger.LogError("Failed to process FantasyPros points for Season: {Season}, Week: {Week}. Status code: {StatusCode}. Response: {Response}", season, week, scoresResponse.StatusCode, error);
            return false;
        }

        _logger.LogInformation("Successfully processed FantasyPros points for Season: {Season}, Week: {Week}.", season, week);
        return true;
    }

    private async Task<List<WaiverClaimSummary>> GetWaiverClaimResults(int season, int week)
    {
        var response = await _http.GetAsync($"api/league/waivers/{season}/{week}");
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError("Failed to retrieve waiver claim results for Season: {Season}, Week: {Week}. Status code: {StatusCode}. Response: {Response}", season, week, response.StatusCode, error);
            return null;
        }

        var resultsJson = await response.Content.ReadAsStringAsync();
        var waiverResults = JsonSerializer.Deserialize<List<WaiverClaimSummary>>(resultsJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (waiverResults is null)
        {
            _logger.LogError("Waiver claim results for Season: {Season}, Week: {Week} could not be parsed.", season, week);
            return null;
        }

        _logger.LogInformation("Successfully retrieved waiver claim results for Season: {Season}, Week: {Week}. Total claims processed: {Count}.", season, week, waiverResults.Count);
        return waiverResults;
    }

    private async Task<bool> FinalizeMatchupsForWeekAsync(int season, int week)
    {
        var response = await _http.PostAsync($"api/league/matchups/{season}/{week}/finalize", null);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError("Failed to finalize matchups for season {Season}, week {Week}. Status code: {StatusCode}. Response: {Response}", season, week, response.StatusCode, error);
            return false;
        }

        _logger.LogInformation("Finalized matchups for season {Season}, week {Week}.", season, week);
        return true;
    }

    private sealed record WaiverAgentSummary(
        string AgentId,
        int Season,
        int Week,
        string Phase,
        int? MyPriority,
        int TotalAgents,
        bool HasPendingClaims,
        IReadOnlyList<WaiverClaimSummary> MyClaims,
        DateTimeOffset? WaiversProcessedAtUtc);

    private sealed record WaiverClaimSummary(
        Guid WaiverClaimId,
        string AgentId,
        int ClaimOrder,
        WaiverPlayerSummary AddPlayer,
        WaiverPlayerSummary? DropPlayer,
        int PriorityAtSubmission,
        string Status,
        string? FailureReason,
        DateTimeOffset SubmittedAtUtc,
        DateTimeOffset? ProcessedAtUtc,
        bool WasSuccessful,
        bool WasSuperseded);

    private sealed record WaiverPlayerSummary(string SleeperPlayerId, string? FullName, string? Team, string? Position);

    private sealed record FantasyProsPointsSyncStatus(int Season, int EndWeek, string? Status, int? RecordCount, DateTimeOffset? CompletedAtUtc);
}