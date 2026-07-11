using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

public class SeasonRunner
{
    private static readonly TimeZoneInfo EasternTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
    private readonly List<FantasyAgent> _agents;
    private readonly ILogger _logger;
    private readonly BlobStorageTools _blobStorageTools = new BlobStorageTools();
    private readonly HttpClient _http;
    private LeagueState _leagueState;

    public SeasonRunner(List<FantasyAgent> agents, ILogger logger, HttpClient http)
    {
        _agents = agents;
        _logger = logger;
        _http = http;
        _leagueState = LeagueStateHelper.GetLeagueStateAsync(_http, _logger).Result;
    }

    // In case for testing I wanted to pass in a custom league state, I can use this constructor instead of the one above.
    public SeasonRunner(List<FantasyAgent> agents, ILogger logger, HttpClient http, LeagueState leagueState)
    {
        _agents = agents;
        _logger = logger;
        _http = http;
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
        var dayOfWeek = easternNow.DayOfWeek;
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

            // First, we process the Yahoo data for the current week to update scores and stats.
            var yahooDataSuccess = await ProcessYahooDataForWeekAsync(_leagueState.Season, _leagueState.Week);
            if (!yahooDataSuccess)
            {
                _logger.LogError($"Yahoo data sync failed for season {_leagueState.Season}, week {_leagueState.Week}; league state will not advance.");
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
            int week = _leagueState.Week + 1;
            _leagueState = await LeagueStateHelper.SetLeagueStateAsync(_leagueState.Season, week, "waiver_window", "season-runner", _http, _logger);

            // TODO: Here I want to tell the agents how they did for the week, and prompt them to reflect on how they did, and what they want to do differently for the next week.
            // This will be a good way to get them to think about their strategy and how they want to approach the next week.
            // Maybe this is a skill? I don't know yet, but I don't think so.

            // Then we will prompt the agents to make waiver claims if they want to.
            var prompt = $"Today is Tuesday, a brand-new week. Use the `weekly-player-management` skill to make any waiver claims for season {_leagueState.Season} week {week}.";
            foreach(var agent in _agents)
            {
                var response = await agent.RunAsync(prompt);
                await DecisionLogger.LogDecisionAsync(agent.GetAgentName()!, week, "Waiver Claim Attempt", response, "Waiver Wire", _logger);
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
                    // Then we will set a prompt for the agents to let them know the waiver claims were successful and to update their rosters accordingly.
                    // This prompt will not ask the agent to make more waiver claims, it is only to update their roster based on the results of the waiver claims.
                    var prompt = $"Today is Wednesday, and all the waiver wire claims have been processed. Use the `roster-management` skill to update your lineup for season {_leagueState.Season} week {_leagueState.Week}.";
                    foreach(var agent in _agents)
                    {
                        var response = await agent.RunAsync(prompt);
                        await DecisionLogger.LogDecisionAsync(agent.GetAgentName()!, _leagueState.Week, "Roster Management", response, "Update Roster", _logger);
                    }
                    // Then we will set the league state to free-agency, and the agents can start making roster moves outside of the waiver wire if they want to.
                    _leagueState = await LeagueStateHelper.SetLeagueStateAsync(_leagueState.Season, _leagueState.Week, "free_agency", "season-runner", _http, _logger);
                }
                else
                {
                    _logger.LogWarning("Failed to process waiver claims for season {Season} week {Week}.", _leagueState.Season, _leagueState.Week);
                }
            }
            if (_leagueState?.Phase == "free_agency")
            {
                // If we want to now, the players can start making roster moves outside of the waiver wire, and then we can prompt the agents to make any roster moves they want to make for the week.
                // TODO: Wire in a prompt for free agency moves, as well as to remind the agents to set their lineups for the week if they haven't already, since some games start on Thursday.
                var prompt =
                $"""
                Today is Wednesday We are in season {_leagueState.Season} week {_leagueState.Week}.
                We are now in the free-agency phase. Use the `weekly-player-management` skill to (optionally) update your lineup for season {_leagueState.Season} week {_leagueState.Week}.
                When you're done, use the `roster-management` skill to update your lineup for season {_leagueState.Season} week {_leagueState.Week}.
                """;
                foreach(var agent in _agents)
                {
                    var response = await agent.RunAsync(prompt);
                    await DecisionLogger.LogDecisionAsync(agent.GetAgentName()!, _leagueState.Week, "Roster Management", response, "Update Roster", _logger);
                }
            }

        }
        if(dayOfWeek == DayOfWeek.Thursday)
        {
            _logger.LogInformation("Running Thursday tasks.");

            // Thursday! This is the day when some games start, so we want to make sure the agents have set their lineups correctly for the games that are happening today.
            // Also, later at night, maybe we run this again to get some scores, as well as lock the players who have played.
            var yahooDataSuccess = await ProcessYahooDataForWeekAsync(_leagueState.Season, _leagueState.Week);
            if (!yahooDataSuccess)
            {
                _logger.LogError($"Yahoo data sync failed for season {_leagueState.Season}, week {_leagueState.Week}; league state will not advance.");
                return;
            }

            // Still in free-agency, so agents can still make roster moves if they want to.
            // Some games start on Thursday, so we will want to make sure the agents have set their lineups before the games start.
            // We will want to do this before games start on Thursday.
            if (_leagueState?.Phase == "free_agency")
            {
                var prompt =
                $"""
                Today is Thursday. We are in season {_leagueState.Season} week {_leagueState.Week}.
                We are still in the free-agency phase, however some players may have games today.
                This is your chance to make sure these players are set correctly for their games today.
                Use the `weekly-player-management` skill to evaluate whether a meaningful free-agent add/drop improves your roster.
                When you're done, use the `roster-management` skill to update your lineup.
                """;
                foreach(var agent in _agents)
                {
                    var response = await agent.RunAsync(prompt);
                    await DecisionLogger.LogDecisionAsync(agent.GetAgentName()!, _leagueState.Week, "Roster Management", response, "Update Roster", _logger);
                }
            }

        }
        if(dayOfWeek == DayOfWeek.Friday)
        {
            _logger.LogInformation("Running Friday tasks.");

            // Friday! This is the day after the Thursday games, so we want to make sure the agents have set their lineups correctly for the games that are happening on Sunday and Monday.
            // Nothing else really happens today.
            // Lets process the Yahoo Weekly data for the Thursday games.
            var yahooDataSuccess = await ProcessYahooDataForWeekAsync(_leagueState.Season, _leagueState.Week);
            if (!yahooDataSuccess)
            {
                _logger.LogError($"Yahoo data sync failed for season {_leagueState.Season}, week {_leagueState.Week}; league state will not advance.");
                return;
            }

            // Still in free-agency, so agents can make any last minute roster moves before the games start on Sunday and Monday.
            // Players who are injured or questionable for the weekend games might get dropped on Friday, so this is the last chance for agents to pick them up before the games start.
            // Players who played on Thursday are stuck where they are.
            if (_leagueState?.Phase == "free_agency")
            {
                var prompt =
                $"""
                Today is Friday. We are in season {_leagueState.Season} week {_leagueState.Week}.
                We are still in the free-agency phase, however some players may have games yesterday, and if so, they will be locked in their current roster spots.
                Use the `weekly-player-management` skill to evaluate whether a meaningful free-agent add/drop improves your roster.
                When you're done, use the `roster-management` skill to update your lineup.
                """;
                foreach(var agent in _agents)
                {
                    var response = await agent.RunAsync(prompt);
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

            // Why not do a sync.
            bool yahooDataSuccess = await ProcessYahooDataForWeekAsync(_leagueState?.Season ?? 0, _leagueState?.Week ?? 0);
            if (yahooDataSuccess)
            {
                _logger.LogInformation("Successfully processed Yahoo data for the week.");
            }

            if (_leagueState?.Phase == "free_agency")
            {
                var prompt =
                $"""
                Today is Saturday. We are in season {_leagueState.Season} week {_leagueState.Week}.
                We are still in the free-agency phase, however some players may have games on Thursday, and if so, they will be locked in their current roster spots.
                This is one of your last chances to make any roster moves before the games start on Sunday and Monday.
                Use the `weekly-player-management` skill to evaluate whether a meaningful free-agent add/drop improves your roster.
                When you're done, use the `roster-management` skill to update your lineup.
                """;
                foreach(var agent in _agents)
                {
                    var response = await agent.RunAsync(prompt);
                    await DecisionLogger.LogDecisionAsync(agent.GetAgentName()!, _leagueState.Week, "Roster Management", response, "Update Roster", _logger);
                }
            }
        }
        if(dayOfWeek == DayOfWeek.Sunday)
        {
            _logger.LogInformation("Running Sunday tasks.");

            // It's Sunday! Games are starting today, so lets do one final oppertunity for the agents to set their lineups for the Sunday games before they start.
            bool yahooDataSuccess = await ProcessYahooDataForWeekAsync(_leagueState?.Season ?? 0, _leagueState?.Week ?? 0);
            if (yahooDataSuccess)
            {
                _logger.LogInformation("Successfully processed Yahoo data for the week.");
            }

            // Only lineup changes are allowed on Sunday, so agents can only set their lineups for the Sunday games, but they can't make any roster moves.
            // Lets set the league state to games_locked, since the games start on Sunday, and we want to make sure the agents have set their lineups before the games start.
            _leagueState = await LeagueStateHelper.SetLeagueStateAsync(_leagueState?.Season, _leagueState?.Week, "games_locked", "season-runner", _http, _logger);

            var prompt =
            $"""
            Today is Sunday. We are in season {_leagueState.Season} week {_leagueState.Week}.
            There are games starting today, so this is your last chance to set your lineups for the Sunday games.
            Preserve every player whose `lockStatus.isLineupMoveLocked` is true. Optimize only players who have not played yet.
            Use the `roster-management` skill to update your lineup.
            """;
            foreach(var agent in _agents)
            {
                var response = await agent.RunAsync(prompt);
                await DecisionLogger.LogDecisionAsync(agent.GetAgentName()!, _leagueState.Week, "Roster Management", response, "Update Roster", _logger);
            }

        }
        if(dayOfWeek == DayOfWeek.Monday)
        {
            // Only lineup changes are allowed on Monday, so agents can only set their lineups for the Monday game, but they can't make any roster moves.
            // We will want to make sure the agents have set their lineups before the game starts on Monday.
            // TODO: Wire in a prompt to remind the agents to set their lineups for the Monday game if they haven't already, since the last game starts on Monday night.
            _logger.LogInformation("Running Monday tasks.");

            bool yahooDataSuccess = await ProcessYahooDataForWeekAsync(_leagueState?.Season ?? 0, _leagueState?.Week ?? 0);
            if (yahooDataSuccess)
            {
                _logger.LogInformation("Successfully processed Yahoo data for the week.");
            }

            var prompt =
            $"""
            Today is Monday. We are in season {_leagueState.Season} week {_leagueState.Week}.
            Most of the games for the week have already been played, but there are still games on Monday.
            This is your last chance to set your lineups for the Monday games.
            Preserve every player whose `lockStatus.isLineupMoveLocked` is true. Optimize only players who have not played yet.
            Use the `roster-management` skill to update your lineup.
            """;
            foreach(var agent in _agents)
            {
                var response = await agent.RunAsync(prompt);
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

    public async Task<string> GetWaiverResultPromptDetailsAsync(string agentId)
    {
        if (string.IsNullOrWhiteSpace(agentId))
        {
            throw new ArgumentException("Agent ID is required.", nameof(agentId));
        }

        if (_leagueState is null)
        {
            return "Waiver wire results could not be loaded because the league state is unavailable.";
        }

        return await GetWaiverResultPromptDetailsAsync(agentId.Trim(), _leagueState.Season, _leagueState.Week);
    }

    // TODO: Here, this might actually be a good way to turn this into a skill to teach the agents how to read and understand waiver wire results,
    // since they will need to know how to read the results of their waiver claims in order to update their rosters accordingly.
    // Right now, we're formatting a document for them, but honestly we could just return the raw JSON and let them parse it themselves,
    // The skill will teach them how.
    private async Task<string> GetWaiverResultPromptDetailsAsync(string agentId, int season, int week)
    {
        var response = await new HttpClient { BaseAddress = new Uri("http://localhost:5000/") }
            .GetAsync($"api/league/waivers/{season}/{week}/agents/{Uri.EscapeDataString(agentId)}/summary");

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError("Failed to load waiver summary for AgentId: {AgentId}, Season: {Season}, Week: {Week}. Status code: {StatusCode}. Response: {Response}", agentId, season, week, response.StatusCode, error);
            return $"Waiver wire results for agent {agentId} could not be loaded from the API.";
        }

        var summaryJson = await response.Content.ReadAsStringAsync();
        var summary = JsonSerializer.Deserialize<WaiverAgentSummary>(summaryJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (summary is null)
        {
            _logger.LogError("Waiver summary response for AgentId: {AgentId}, Season: {Season}, Week: {Week} could not be deserialized.", agentId, season, week);
            return $"Waiver wire results for agent {agentId} could not be parsed from the API response.";
        }

        var promptDetails = new StringBuilder();
        promptDetails.AppendLine($"Waiver wire results for {summary.AgentId} for season {summary.Season} week {summary.Week}:");
        promptDetails.AppendLine($"- League phase: {summary.Phase}");
        promptDetails.AppendLine(summary.MyPriority.HasValue
            ? $"- Current waiver priority: {summary.MyPriority.Value} of {summary.TotalAgents}"
            : $"- Current waiver priority: unavailable (total agents: {summary.TotalAgents})");
        promptDetails.AppendLine(summary.WaiversProcessedAtUtc.HasValue
            ? $"- Waiver processing completed at: {summary.WaiversProcessedAtUtc.Value:u}"
            : "- Waiver processing has not completed yet.");
        promptDetails.AppendLine(summary.HasPendingClaims
            ? "- You still have pending waiver claims."
            : "- You do not have any pending waiver claims.");

        if (summary.MyClaims.Count == 0)
        {
            promptDetails.AppendLine("- You did not submit any waiver claims for this week.");
            return promptDetails.ToString().TrimEnd();
        }

        promptDetails.AppendLine("- Claims:");
        foreach (var claim in summary.MyClaims.OrderBy(claim => claim.ClaimOrder))
        {
            var addPlayer = FormatWaiverPlayer(claim.AddPlayer);
            var dropPlayer = claim.DropPlayer is null ? "No drop required" : FormatWaiverPlayer(claim.DropPlayer);

            promptDetails.AppendLine($"  - Claim {claim.ClaimOrder}: add {addPlayer}; drop {dropPlayer}.");
            promptDetails.AppendLine($"    Status: {claim.Status}. Priority at submission: {claim.PriorityAtSubmission}.");

            if (claim.WasSuccessful)
                promptDetails.AppendLine("    Outcome: This claim succeeded and the add/drop was applied.");
            else if (claim.WasSuperseded)
                promptDetails.AppendLine("    Outcome: This claim was superseded by another successful claim for the same waiver period.");

            if (!string.IsNullOrWhiteSpace(claim.FailureReason))
                promptDetails.AppendLine($"    Failure reason: {claim.FailureReason}");

            promptDetails.AppendLine($"    Submitted at: {claim.SubmittedAtUtc:u}.");
            if (claim.ProcessedAtUtc.HasValue)
                promptDetails.AppendLine($"    Processed at: {claim.ProcessedAtUtc.Value:u}.");
        }

        return promptDetails.ToString().TrimEnd();
    }

    private async Task<bool> ProcessYahooDataForWeekAsync(int season, int week)
    {
        var latestSyncResponse = await _http.GetAsync($"api/sync/yahoo/latest?season={season}&week={week}");
        if (latestSyncResponse.IsSuccessStatusCode)
        {
            var latestSyncJson = await latestSyncResponse.Content.ReadAsStringAsync();
            var latestSync = JsonSerializer.Deserialize<YahooSyncRunSummary>(latestSyncJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (latestSync is null)
            {
                _logger.LogError("Yahoo sync status for season {Season}, week {Week} could not be parsed.", season, week);
                return false;
            }

            if (string.Equals(latestSync.Status, "Succeeded", StringComparison.OrdinalIgnoreCase)
                && latestSync.CompletedAtUtc.HasValue
                && DateTimeOffset.UtcNow - latestSync.CompletedAtUtc.Value < TimeSpan.FromHours(2))
            {
                _logger.LogInformation("Skipping Yahoo sync for season {Season}, week {Week}; the last successful sync completed at {CompletedAtUtc}.", season, week, latestSync.CompletedAtUtc.Value);
                return true;
            }
        }
        else if (latestSyncResponse.StatusCode != System.Net.HttpStatusCode.NotFound)
        {
            var error = await latestSyncResponse.Content.ReadAsStringAsync();
            _logger.LogError("Failed to load Yahoo sync status for season {Season}, week {Week}. Status code: {StatusCode}. Response: {Response}", season, week, latestSyncResponse.StatusCode, error);
            return false;
        }

        var yahooResponse = await _http.PostAsync($"api/sync/yahoo/weekly?week={week}&season={season}&force=false", null);
        if (!yahooResponse.IsSuccessStatusCode)
        {
            var error = await yahooResponse.Content.ReadAsStringAsync();
            _logger.LogError("Failed to process Yahoo data for Season: {Season}, Week: {Week}. Status code: {StatusCode}. Response: {Response}", season, week, yahooResponse.StatusCode, error);
            return false;
        }

        _logger.LogInformation("Successfully processed Yahoo data for Season: {Season}, Week: {Week}.", season, week);
        return true;
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


    private static string FormatWaiverPlayer(WaiverPlayerSummary player)
    {
        var name = string.IsNullOrWhiteSpace(player.FullName) ? player.SleeperPlayerId : player.FullName;
        var teamAndPosition = string.Join(" ", new[] { player.Team, player.Position }.Where(value => !string.IsNullOrWhiteSpace(value)));
        return string.IsNullOrWhiteSpace(teamAndPosition)
            ? $"{name} ({player.SleeperPlayerId})"
            : $"{name} ({player.SleeperPlayerId}, {teamAndPosition})";
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

    private sealed record YahooSyncRunSummary(string Status, DateTimeOffset? CompletedAtUtc);
}