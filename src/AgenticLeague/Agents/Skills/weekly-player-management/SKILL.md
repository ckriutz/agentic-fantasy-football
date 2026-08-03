---
name: weekly-player-management
description: Evaluate roster deficiencies and improve the team through waiver claims or free agency. Use for waiver wire, free-agent adds, weekly roster improvement, injury replacement, bye-week coverage, roster deficiencies, or add/drop decisions. Do not use for setting a lineup without evaluating acquisitions.
metadata:
  author: agentic-league
  version: "1.0"
  domain: fantasy-football
---

# Weekly Player Management

Improve the roster only when an available player materially addresses a current or near-term need. This skill handles player acquisition decisions; use the `roster-management` skill after a successful acquisition when the lineup needs to change.

## Scope and hard constraints

- Acquisition is optional. A well-supported no-move outcome is correct.
- Never acquire a player before confirming the league phase.
- Use only the phase-aware acquisition tools: `SubmitWaiverClaimForCurrentWeek` during `waiver_window`, and `AddFreeAgentForCurrentWeek` during `free_agency`.
- Do **not** use `add_free_agent`, `AddPlayerToRoster`, `RemovePlayerFromRoster`, or any explicit-week add variant. They either do not exist for weekly play or bypass the waiver/free-agent lifecycle, and calling them just returns errors.
- Make **at most one successful acquisition per run**. Once an add or claim succeeds, stop acquiring and write the summary. Never chain a second add in the same run.
- Attempt an acquisition **once**. Check the result `ok` field: if `false`, read `error.code`, `error.message`, and `error.nextStep`, apply `nextStep` only if it is a single safe correction, and otherwise stop. Do **not** retry the same call or cycle through other players hoping one succeeds.
- Do not drop a healthy, needed starter merely to make a speculative addition.
- Do not drop a lineup-locked player or a player whose add/drop lock status prevents the transaction.
- Do not make player moves during phases other than `waiver_window` or `free_agency`.
- Use the exact `agentId` supplied by the task for every roster and waiver tool.
- The run is complete only when you output the decision summary as visible text. Never end on a tool call.

## Required tools

Tool names below are shown in PascalCase; the runtime exposes the league (MCP) tools in snake_case (e.g. `AddFreeAgentForCurrentWeek` is `add_free_agent_for_current_week`, `GetMyRoster` is `get_my_roster`). They are the same tools — match on either form.

| Tool | Purpose |
|------|---------|
| `GetLeagueState` | Establish the authoritative season, week, and phase. |
| `GetMyWaiverStatus` | Check priority, existing claims, and current claim outcomes. |
| `ReadAgentBootstrap` / `WriteAgentBootstrap` | Use and maintain the durable game plan and decision log. |
| `GetMyRoster` | Find deficiencies, roster capacity, drop candidates, and locks. |
| `GetAvailablePlayers` | Find unrostered candidates by position. |
| `SearchWeb` | Confirm current injuries, roles, depth charts, and meaningful news. |
| `SubmitWaiverClaimForCurrentWeek` | Submit a waiver claim during `waiver_window`. |
| `AddFreeAgentForCurrentWeek` | Add an available player immediately during `free_agency`. |

## Workflow

### 1. Establish the transaction window

1. Call `GetLeagueState` first and treat its `season`, `week`, and `phase` as authoritative.
2. Call `GetMyWaiverStatus(agentId)` to inspect waiver priority, pending claims, and prior results.
3. If `phase` is neither `waiver_window` nor `free_agency`, make no acquisition. Explain the phase and end with the required summary.
4. If `phase` is `waiver_window` and `HasPendingClaims` is true, do not replace your claim unless current roster information or research justifies a better one. `SubmitWaiverClaimForCurrentWeek` replaces your existing pending claim for that week.

### 2. Diagnose roster needs

1. Call `ReadAgentBootstrap(agentId)` for the team's durable strategy, prior decisions, and known needs.
2. Call `GetMyRoster(agentId)`.
3. Identify concrete deficiencies, ordered by urgency:
   1. A starter who is `Out`, `IR`, `PUP`, suspended, or has a long-term injury.
   2. An empty or unfillable current-week starting position because of an injury or bye.
   3. A thin position with no viable backup for an upcoming bye or likely absence.
   4. A persistently underperforming or demoted player when an available player is a genuine upgrade.
4. Do not treat a short-term `Questionable` tag or one poor game as sufficient reason to cut a valuable player. Use `SearchWeb` when current status, role, or recovery timeline affects the decision.
5. Count the roster. The league has a 16-player maximum. A roster of 16 requires a valid drop for an acquisition; otherwise no drop is needed where the specific tool supports it.

### 3. Find and assess candidates

1. Call `GetAvailablePlayers` for the position or positions needed most. Search a reasonable candidate pool, not only one player.
2. Compare candidates with the player they would replace using:
   1. Availability for the current and upcoming weeks: not on bye, injury outlook, and expected snaps.
   2. Role security: depth-chart position and recent role changes.
   3. `projectedFantasyPoints`.
   4. `rankAverage` (lower is better; FantasyPros consensus rank when present).
   5. `positionRank` (lower/better positional label, e.g. `RB1` over `RB3`).
   6. `tier` (lower is better FantasyPros tier).
   7. `playerOwnedAverage` (higher is better ownership %).
   8. `searchRank` (lower is better; treat null or `9999999` as unranked; fallback when FantasyPros ranks are missing).
   9. Recent `weeklyPoints`, without overweighting one outlier.
   10. `lastSeasonFantasyPoints` and `auctionValue` as secondary context.
3. Use `SearchWeb` for the leading candidates when injury news, depth-chart role, target share, or a recent breakout determines whether the move is worthwhile.
4. Do not add a player merely because they are available. The candidate must fill an identified need or be a material improvement over the proposed drop.

### 4. Choose a valid transaction path

#### Waiver window

- Use `SubmitWaiverClaimForCurrentWeek(agentId, addSleeperPlayerId, dropSleeperPlayerId)` to submit a single claim for the current week.
- If the roster is full, pass a valid rostered `dropSleeperPlayerId`. If the roster has an open slot, pass `null`; the tool supports a no-drop claim, so do not invent a drop.
- Each submission replaces your existing pending claim for the week, so submit your single best add/drop pairing.
- Do not submit a claim if no candidate is a genuine improvement.

#### Free agency

- Use `AddFreeAgentForCurrentWeek(agentId, addSleeperPlayerId, dropSleeperPlayerId)`. This is the **only** free-agency add tool — do not call `add_free_agent` or an explicit-week variant.
- Pass `dropSleeperPlayerId` only when the roster is full.
- Make at most one successful add per run. Check the result `ok` field: if `true`, you are done acquiring; if `false`, read `error.nextStep` and either apply that single correction or stop — do not loop through alternate players.

### 5. Follow through and preserve memory

1. Review the transaction result. A submitted waiver claim is pending, not a successful roster addition.
2. After a free-agent add succeeds, call `GetMyRoster(agentId)` to confirm the add/drop. A new player begins on `BN`.
3. If a successful addition changes who should start, invoke the `roster-management` skill. Do not use `AutoSetLineup`.
4. Optionally update `bootstrap.md` after a meaningful completed add, drop, or waiver-claim submission. Add one concise dated note under `## Decision Log` with the phase, action, player names/IDs, and rationale.
5. Update `## Strategy Updates` only when the action creates a lasting strategic implication, such as a new position need or changed risk posture.
6. Preserve existing game-plan content. Do not replace the document with only the newest transaction.

## Required decision summary

End every run with this exact structure:

```markdown
## Weekly player management (Week {week})
**Loaded skill:** weekly-player-management

**Phase:** {phase}

**Outcome:** added | waiver_claims_submitted | no_change | blocked

**Action:** <one-line factual outcome>

**Roster need:**
- <identified deficiency, or "None">

**Candidates evaluated:**
- Player (`sleeperId`) — add/drop case and key evidence
- (or "None")

**Transaction:**
- <tool called, player added/claimed, player dropped, and result>
- (or "No move")

**Why:**
- <evidence-based rationale>

**Open risks:**
- <pending waiver result, injury uncertainty, bye, or "None">
```

## Common failure modes

| Mistake | Correct behavior |
|---------|------------------|
| Calling an add tool before reading phase | Call `GetLeagueState` first. |
| Adding during `waiver_window` with a free-agent tool | Submit a waiver claim instead. |
| Adding during `free_agency` with waiver claims | Use `AddFreeAgentForCurrentWeek`. |
| Replacing pending claims accidentally | Resubmit only when intentionally replacing the full ordered list. |
| Treating a submitted claim as an acquired player | It is pending until waiver processing reports success. |
| Using direct roster add/remove tools | Use the phase-aware waiver tools for acquisitions. |
| Dropping an asset for a one-week speculative add | Require a meaningful need and material upgrade. |
| Retrying a failed add with different players | Attempt once; on `ok: false` follow `error.nextStep` or stop and report. |
| Calling `add_free_agent` or `AddPlayerToRoster` | Use `AddFreeAgentForCurrentWeek` only. |
| Making a second add after one already succeeded | One successful acquisition per run, then summarize. |
| Leaving a newly added starter candidate on the bench | Run `roster-management` after a confirmed successful add when lineup changes are needed. |
