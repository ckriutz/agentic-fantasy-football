using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

public class SeasonRunner
{
    private readonly List<FantasyAgent> _agents;
    private readonly ILogger _logger;
    private readonly BlobStorageTools _blobStorageTools = new BlobStorageTools();

    public SeasonRunner(List<FantasyAgent> agents, ILogger logger)
    {
        _agents = agents;
        _logger = logger;
    }

    public async Task RunAsync()
    {
        _logger.LogInformation("Season runner is starting.");
        var leaugeState = await GetLeagueStateAsync();
        if (leaugeState is null)
        {
            _logger.LogError("Season runner cannot continue because league state is unavailable.");
            return;
        }

        // What we do determines what day of the week it is in the league, so we'll want to get the day of the week.
        var dayOfWeek = DateTime.UtcNow.DayOfWeek;
        _logger.LogInformation("Today is {DayOfWeek}.", dayOfWeek);

        var waiverWirePrompt = await _blobStorageTools.GetPromptFromBlobStorageAsync("FantasyAgent.waiver-claim.md");

        if(dayOfWeek == DayOfWeek.Tuesday)
        {
            // This is where we will first see how the week went and update wins/losses and any other stats.
            if (leaugeState is not null && leaugeState.Week > 0)
            {
                var yahooDataSuccess = await ProcessYahooDataForWeekAsync(leaugeState.Season, leaugeState.Week);
                if (!yahooDataSuccess)
                {
                    _logger.LogError(
                        "Yahoo data sync failed for season {Season}, week {Week}; league state will not advance.",
                        leaugeState.Season,
                        leaugeState.Week);
                    return;
                }

                var finalized = await FinalizeMatchupsForWeekAsync(leaugeState.Season, leaugeState.Week);
                if (!finalized)
                {
                    _logger.LogError(
                        "Matchup finalization failed for season {Season}, week {Week}; league state will not advance.",
                        leaugeState.Season,
                        leaugeState.Week);
                    return;
                }
            }

            // Once that is done, this is really the first day of the week, so we will make sure the league state is updated to the current week.
            // Set the league state to waiver-wire.
            if (leaugeState is null)
            {
                _logger.LogError("League state became unavailable before the weekly transition.");
                return;
            }

            int week = leaugeState.Week + 1;
            leaugeState = await SetLeagueStateAsync(leaugeState.Season, week, "waiver_window", "season-runner");

            // Then we will prompt the agents to make waiver claims if they want to.
            foreach(var agent in _agents)
            {
                var response = await agent.RunAsync(waiverWirePrompt);
                await DecisionLogger.LogDecisionAsync(agent.GetAgentName()!, week, "Waiver Claim Attempt", response, "Waiver Wire", _logger);
            }

            // No reason on this day, to prompt the agents to set their lineups, since the games don't start until Thursday, and they might want to make waiver claims before setting their lineups.
        }
        if(dayOfWeek == DayOfWeek.Wednesday)
        {
            // Here we will process the waiver claims, and let the agents know which claims were successful and which were not, and update their rosters accordingly.
            // First, lets check to see if the status is still on waiver-wire, if it is, then we will process the waiver claims, and then update the league state to free-agency.
            if (leaugeState?.Phase == "waiver_window")
            {
                bool success = await ProcessWaiverClaimsAsync(leaugeState.Season, leaugeState.Week);
                if (success)
                {
                    // TODO: Set a prompt for the agents to let them know which of their waiver claims were successful and which were not, and update their rosters accordingly.
                    // Then we will set the league state to free-agency, and the agents can start making roster moves outside of the waiver wire if they want to.
                    leaugeState = await SetLeagueStateAsync(leaugeState.Season, leaugeState.Week, "free_agency", "season-runner");
                }
            }
            if (leaugeState?.Phase == "free_agency")
            {
                // If we want to now, the players can start making roster moves outside of the waiver wire, and then we can prompt the agents to make any roster moves they want to make for the week.
                // TODO: Wire in a prompt for free agency moves, as well as to remind the agents to set their lineups for the week if they haven't already, since some games start on Thursday.
            }

        }
        if(dayOfWeek == DayOfWeek.Thursday)
        {
            // Still in free-agency, so agents can still make roster moves if they want to.
            // Some games start on Thursday, so we will want to make sure the agents have set their lineups before the games start.
            // We will want to do this before games start on Thursday.
            if (leaugeState?.Phase == "free_agency")
            {
                // If we want to now, the players can start making roster moves outside of the waiver wire, and then we can prompt the agents to make any roster moves they want to make for the week.
                // TODO: Wire in a prompt for free agency moves, as well as to remind the agents to set their lineups for the week if they haven't already, since some games start on Thursday.
            }

        }
        if(dayOfWeek == DayOfWeek.Friday)
        {
            // Lets process the Yahoo Weekly data for the Thursday games.
            // TODO: Process the Yahoo Weekly data for the Thursday games, and update the player stats and team stats accordingly.

            // Still in free-agency, so agents can make any last minute roster moves before the games start on Sunday and Monday.
            // Players who are injured or questionable for the weekend games might get dropped on Friday, so this is the last chance for agents to pick them up before the games start.
            // Players who played on Thursday are stuck where they are.
            if (leaugeState?.Phase == "free_agency")
            {
                // If we want to now, the players can start making roster moves outside of the waiver wire, and then we can prompt the agents to make any roster moves they want to make for the week.
                // TODO: Wire in a prompt for free agency moves, as well as to remind the agents to set their lineups for the week if they haven't already, since some games start on Thursday.
            }
        }
        if(dayOfWeek == DayOfWeek.Saturday)
        {

            // Still in free-agency, so agents can make any last minute roster moves before the games start on Sunday and Monday.
            // Players who are injured or questionable for the weekend games might get dropped on Friday, so this is the last chance for agents to pick them up before the games start.
            // Players who played on Thursday are stuck where they are.
            if (leaugeState?.Phase == "free_agency")
            {
                // If we want to now, the players can start making roster moves outside of the waiver wire, and then we can prompt the agents to make any roster moves they want to make for the week.
                // TODO: Wire in a prompt for free agency moves, as well as to remind the agents to set their lineups for the week if they haven't already, since some games start on Thursday.
            }
        }
        if(dayOfWeek == DayOfWeek.Sunday)
        {
            _logger.LogInformation("Running Sunday tasks.");
            bool yahooDataSuccess = await ProcessYahooDataForWeekAsync(leaugeState?.Season ?? 0, leaugeState?.Week ?? 0);
            if (yahooDataSuccess)
            {
                _logger.LogInformation("Successfully processed Yahoo data for the week.");
            }
            // Only lineup changes are allowed on Sunday, so agents can only set their lineups for the Sunday games, but they can't make any roster moves.
            // Lets set the league state to games_locked, since the games start on Sunday, and we want to make sure the agents have set their lineups before the games start.
            leaugeState = await SetLeagueStateAsync(leaugeState?.Season, leaugeState?.Week, "games_locked", "season-runner");
            foreach(var agent in _agents)
            {
                var prompt = GetPromptForSettingRoster(agent.GetAgentName(), leaugeState?.Week ?? 0);
                var response = await agent.RunAsync(prompt);
                await DecisionLogger.LogDecisionAsync(agent.GetAgentName()!, leaugeState?.Week ?? 0, "Set Lineup for Sunday Games", response, "Lineup Setting", _logger);
            }

        }
        if(dayOfWeek == DayOfWeek.Monday)
        {
            // Only lineup changes are allowed on Monday, so agents can only set their lineups for the Monday game, but they can't make any roster moves.
            // We will want to make sure the agents have set their lineups before the game starts on Monday.
            // TODO: Wire in a prompt to remind the agents to set their lineups for the Monday game if they haven't already, since the last game starts on Monday night.
            _logger.LogInformation("Running Monday tasks.");

            bool yahooDataSuccess = await ProcessYahooDataForWeekAsync(leaugeState?.Season ?? 0, leaugeState?.Week ?? 0);
            if (yahooDataSuccess)
            {
                _logger.LogInformation("Successfully processed Yahoo data for the week.");
            }

            foreach(var agent in _agents)
            {
                var prompt = GetPromptForSettingRoster(agent.GetAgentName(), leaugeState?.Week ?? 0);
                var response = await agent.RunAsync(prompt);
                _logger.LogInformation("Monday lineup response from {AgentName}: {Response}", agent.GetAgentName(), response);
                await DecisionLogger.LogDecisionAsync(agent.GetAgentName()!, leaugeState?.Week ?? 0, "Set Lineup for Monday Games", response, "Lineup Setting", _logger);
            }
        }
    }

    private async Task<LeagueState?> GetLeagueStateAsync()
    {
        var leagueStateResponse = await new HttpClient { BaseAddress = new Uri("http://localhost:5000/") }.GetAsync("api/league/state");
        if (!leagueStateResponse.IsSuccessStatusCode)
        {
            _logger.LogError("Failed to load league state. Status code: " + leagueStateResponse.StatusCode);
            return null;
        }

        var leagueStateJson = await leagueStateResponse.Content.ReadAsStringAsync();
        var leagueState = JsonSerializer.Deserialize<LeagueState>(leagueStateJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (leagueState == null)
        {
            _logger.LogError("League state response was not a JSON object.");
            return null;
        }

        var phase = leagueState.Phase;
        if (string.IsNullOrWhiteSpace(phase))
        {
            _logger.LogError("League state phase was missing from the API response.");
            return null;
        }

        return leagueState;
    }

    private async Task<LeagueState?> SetLeagueStateAsync(int? season, int? week, string phase, string updatedBy)
    {
        var response = await new HttpClient { BaseAddress = new Uri("http://localhost:5000/") }.PutAsJsonAsync("api/league/state", new
        {
            season,
            week,
            phase,
            updatedBy
        });

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError("Failed to set league state for Season: {Season}, Week: {Week}, Phase: {Phase}. Status code: {StatusCode}. Response: {Response}", season, week, phase, response.StatusCode, error);
            return null;
        }

        var leagueStateJson = await response.Content.ReadAsStringAsync();
        var leagueState = JsonSerializer.Deserialize<LeagueState>(leagueStateJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (leagueState == null)
        {
            _logger.LogError("League state response was not a JSON object.");
            return null;
        }

        _logger.LogInformation("Successfully set league state to Season: {Season}, Week: {Week}, Phase: {Phase}, UpdatedBy: {UpdatedBy}.", season, week, phase, updatedBy);
        return leagueState;
    }

    private async Task<bool> ProcessWaiverClaimsAsync(int season, int week)
    {
        // This is where we will process the waiver claims.
        var waiverResponse = await new HttpClient { BaseAddress = new Uri("http://localhost:5000/") }.PostAsync($"api/league/waivers/{season}/{week}/process", null);
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
            throw new ArgumentException("Agent ID is required.", nameof(agentId));

        var leagueState = await GetLeagueStateAsync();
        if (leagueState is null)
            return "Waiver wire results could not be loaded because the league state is unavailable.";

        return await GetWaiverResultPromptDetailsAsync(agentId.Trim(), leagueState.Season, leagueState.Week);
    }

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
        // This is where we will process the Yahoo data for the week.
        var yahooResponse = await new HttpClient { BaseAddress = new Uri("http://localhost:5000/") }.PostAsync($"api/sync/yahoo/weekly?week={week}&season={season}&force=false", null);
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
        var response = await new HttpClient { BaseAddress = new Uri("http://localhost:5000/") }
            .PostAsync($"api/league/matchups/{season}/{week}/finalize", null);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError(
                "Failed to finalize matchups for season {Season}, week {Week}. Status code: {StatusCode}. Response: {Response}",
                season,
                week,
                response.StatusCode,
                error);
            return false;
        }

        _logger.LogInformation("Finalized matchups for season {Season}, week {Week}.", season, week);
        return true;
    }

    private string GetPromptForSettingRoster(string agentId, int week)
    {
        var lineupPrompt = $"""
        Set your lineup for NFL week {week}.

        Your goal is to maximize points from the players already on your roster for this week's remaining games.
        Do not add or remove players in this turn. Only adjust lineup slots.

        Use `GetMyRoster` with agentId `{agentId}` to review your full roster first.
        For each player, pay attention to `position`, `slotType`, `isStarter`, `byeWeek`, `injuryStatus`, `injury_body_part`, and `weeklyPoints`.

        Use these rules when making decisions:
        - If `byeWeek` is {week}, the player should be on the bench.
        - If `injuryStatus` is `Out`, the player should be on the bench.
        - If `injuryStatus` is `Questionable`, `Doubtful`, or otherwise uncertain, use `SearchWeb` to research whether the player is expected to play and whether they are likely to be limited.
        - If `injuryStatus` is empty or clearly healthy, you usually do not need external research.
        - `injury_body_part` tells you what part of the body is affected.
        - `weeklyPoints` is a week-by-week scoring history you can use as one signal for recent performance.

        After your research, use `SetPlayerSlot` with agentId `{agentId}` to put the best available players into the valid starting slots: `QB1`, `RB1`, `RB2`, `WR1`, `WR2`, `TE1`, `FLEX1`, `K1`, and `DEF1`.
        Use `BN` for players who should be benched.

        If the current lineup is already the best one based on the information you have, it is okay to make no changes.
        When you are done, respond with the lineup decisions you made and briefly explain any notable start/sit calls.
        """;

        return lineupPrompt;
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

    private sealed record WaiverPlayerSummary(
        string SleeperPlayerId,
        string? FullName,
        string? Team,
        string? Position);
}