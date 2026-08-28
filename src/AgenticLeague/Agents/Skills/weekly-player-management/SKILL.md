---
name: weekly-player-management
description: Evaluate roster deficiencies and improve the team through waiver claims or free agency. Use for waiver wire, free-agent adds, weekly roster improvement, injury replacement, bye-week coverage, roster deficiencies, or add/drop decisions. Do not use for setting a lineup without evaluating acquisitions.
metadata:
  author: agentic-league
  version: "1.1"
  domain: fantasy-football
---

# Weekly Player Management

Improve the roster only when an available player materially addresses a current or near-term need. This skill handles player acquisition decisions; use the `roster-management` skill after a successful acquisition when the lineup needs to change.

First thing to check is the league phase by using the `GetLeagueState` tool. If it is `games_locked` then there is nothing for you to do since players cannot be added or dropped during this phase. If the league phase is `waiver_window` or `free_agency` then you can continue to evaluate your roster.

## Scope and hard constraints

- Acquisition is optional. A well-supported no-move outcome is correct.
- Never acquire a player before confirming the league phase.
- Use only `MakeRosterMove` for additions, drops, and waiver claims. The server applies the correct behavior for the current league phase.
- Make **at most one successful acquisition per run**. Once an add or claim succeeds, stop acquiring and write the summary. Never chain a second add in the same run.
- Attempt an acquisition, and then check the result `ok` field: if `false`, read `error.code`, `error.message`, and `error.nextStep`, apply `nextStep` only if it is a single safe correction, and otherwise stop. Do **not** retry the same call or cycle through other players hoping one succeeds.
- Do not drop a healthy, needed starter merely to make a speculative addition.
- Do not drop a lineup-locked player or a player whose add/drop lock status prevents the transaction as calling them just returns errors.
- Use the exact `agentId` supplied by the task for every roster and waiver tool.
- The run is complete only when you output the decision summary as visible text. Never end on a tool call.

## Required tools

Tool names below are shown in PascalCase; the runtime exposes the league (MCP) tools in snake_case (e.g. `MakeRosterMove` is `make_roster_move`, `GetMyRoster` is `get_my_roster`). They are the same tools — match on either form.

| Tool | Purpose |
|------|---------|
| `GetLeagueState` | Establish the authoritative season, week, and phase. |
| `GetMyWaiverStatus` | Check priority, existing claims, and current claim outcomes during the `waiver_window` phase. |
| `ReadAgentBootstrap` / `WriteAgentBootstrap` | Use and maintain the memory and lessons learned. |
| `GetMyRoster` | Find deficiencies, roster capacity, drop candidates, and locks. |
| `GetAvailablePlayers` | Find unrostered candidates by position. |
| `SearchWeb` | Confirm current injuries, roles, depth charts, and meaningful news. |
| `MakeRosterMove` | Make one phase-aware add, drop, or replacement. Returns `pending_waiver` during the waiver window and `completed` after an immediate draft or free-agent move. |

## Workflow

### 1. Establish the transaction window

1. Call `GetLeagueState` first and treat its `season`, `week`, and `phase` as authoritative.
2. Call `GetMyWaiverStatus(agentId)` to inspect waiver priority, pending claims, and prior results.
3. If `phase` is neither `waiver_window` nor `free_agency`, make no acquisition. Explain the phase and end with the required summary.
4. If `phase` is `waiver_window` and `HasPendingClaims` is true, do not replace your claim unless current roster information or research justifies a better one. Calling `MakeRosterMove` with an add during this phase replaces your existing pending claim for that week.

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

1. Call `GetAvailablePlayers` for the position or positions needed most. **Always pass `position`** — results are ordered by projected season points, which is only comparable within a position. Search a reasonable candidate pool, not only one player.
2. Compare candidates with the player they would replace using, in priority order:
   1. Availability for the current and upcoming weeks: not on bye, injury outlook, and expected snaps.
   2. `projectedFantasyPoints` (higher is better; the most consistently populated signal).
   3. `rankAverage` (lower is better; consensus rank within the player's own position).
   4. Role security: `depth_chart_order` (1 ≈ starter) and recent role changes.
   5. Recent `weeklyPoints`, without overweighting one outlier.
3. **A missing ranking field means unranked, not bad.** `rankAverage`, `positionRank`, `tier`, and `playerOwnedAverage` come from a rankings source that only covers players with prior-season history, so first-year players are always absent from all four. When they are missing, judge the player on `projectedFantasyPoints`, `depth_chart_order`, and `SearchWeb`. Never treat an absent ranking field as evidence against a player, and never prefer a ranked player over an unranked one on that basis alone.
4. Use `SearchWeb` for the leading candidates when injury news, depth-chart role, target share, or a recent breakout determines whether the move is worthwhile. This matters most for first-year players, where research is the only role evidence available.
5. Do not add a player merely because they are available. The candidate must fill an identified need or be a material improvement over the proposed drop.

### 4. Make one phase-aware roster move

- Call `MakeRosterMove(agentId, addSleeperPlayerId, dropSleeperPlayerId)` for both waiver and free-agency moves.
- If the roster is full, pass a valid rostered `dropSleeperPlayerId`. If the roster has an open slot, pass `null`; do not invent a drop.
- During `waiver_window`, result status `pending_waiver` means the claim was submitted and the roster has not changed yet. Each submission replaces the existing pending claim for the week.
- During `free_agency`, result status `completed` means the add/drop happened immediately.
- Do not make a move if no candidate is a genuine improvement.
- Make at most one successful call per run. If `ok` is false, read `error.nextStep` and either apply that single correction or stop; do not loop through alternate players.

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
| Replacing pending claims accidentally | Resubmit only when intentionally replacing the full ordered list. |
| Treating a submitted claim as an acquired player | It is pending until waiver processing reports success. |
| Choosing a phase-specific mutation tool | Always use `MakeRosterMove`; the server chooses draft, waiver, or free-agency behavior. |
| Dropping an asset for a one-week speculative add | Require a meaningful need and material upgrade. |
| Skipping a player because `rankAverage` or `tier` is missing | Missing ranking fields mean unranked, not bad. Judge on projections, depth chart, and research. |
| Calling `GetAvailablePlayers` with no `position` | Always pass `position`; projected points only compare within a position. |
| Retrying a failed add with different players | Attempt once; on `ok: false` follow `error.nextStep` or stop and report. |
| Calling an old add, drop, free-agent, or waiver mutation tool | Use `MakeRosterMove` only. |
| Making a second add after one already succeeded | One successful acquisition per run, then summarize. |
| Leaving a newly added starter candidate on the bench | Run `roster-management` after a confirmed successful add when lineup changes are needed. |
