# Step 4 — Create the League System

## Problem Statement

The draft and agent infrastructure exist, but there is no system to actually *run a season*. There's no concept of a weekly schedule, matchups, starters vs bench, standings, waivers, or trades. The roster is a flat list with no starter/bench distinction. The system needs to go from "draft complete" to "autonomous season simulation" with no input from me.

## Proposed Approach

Build the league system in layers, bottom-up:

1. **Data layer first** (LeagueAPI) — new entities, services, API endpoints, and MCP tools
2. **Orchestration layer** (AgenticLeague) — a `SeasonRunner` that drives weekly cycles and prompts agents

The system models an NFL fantasy season: 14-week regular season, waiver processing between weeks, agent-driven roster management, and automated scoring/standings. Everything runs autonomously after bootstrap + draft.

---

## Design Decisions (Confirmed)

1. **Lineup model**: Slot-based — strict positional slots (QB1, RB1, RB2, WR1, WR2, TE1, FLEX1, K1, DEF1, BN1–BN6). Enforced in DB.
2. **Waiver wire**: Rolling priority — resets to last after a successful claim.
3. **Trades**: Deferred — not included in Step 4. Agents can only add/drop via waivers and free agency.
4. **Agent prompting**: At least daily — agents are prompted daily to manage their roster (set lineup, react to injuries, evaluate free agents). Post-week waiver claims are a separate prompt. Injury-triggered reactive prompts for affected owners.
5. **Playoffs**: Included — 6-team, 3-week bracket (Weeks 15–17). Top 6 by record (points-for tiebreaker).
6. **Season runner**: Same Program.cs flow — bootstrap → draft → season, all sequential.

---

## Implementation Todos

### 4.1 — Add Lineup Slot Model (LeagueAPI)

**What:** Add a `SlotType` string column to `RosterAssignmentEntity` to enforce strict positional slots. Valid values: `QB1`, `RB1`, `RB2`, `WR1`, `WR2`, `TE1`, `FLEX1`, `K1`, `DEF1`, `BN1`–`BN6`. A player's slot determines if they're a starter (non-BN) or benched (BN*).

**Why:** The current roster is a flat list. Fantasy scoring only counts starters. Agents need to "set their lineup" each week, and the scoring engine needs to know which players are active.

**Validation rules:**
- A player can only be assigned to a slot matching their position (e.g., a WR can go in WR1, WR2, FLEX1, or BN*)
- FLEX1 accepts RB, WR, or TE
- Each slot can only hold one player
- Agents must have exactly 9 starters to lock a lineup

**Where:** 
- `LeagueAPI/Models/RosterAssignmentEntity.cs` — add `SlotType` string field (nullable for migration compat, default BN slots)
- New EF migration
- Update `PostgresRosterStore` — slot validation, assignment, swap logic
- New MCP tools: `SetPlayerSlot(agentId, sleeperPlayerId, slotType)`, `AutoSetLineup(agentId)` (best-projected auto-fill)

### 4.2 — Schedule Generator (LeagueAPI)

**What:** Create a round-robin schedule for 10 teams over 14 weeks. Each team plays every other team at least once, with some rematches to fill 14 weeks (10 teams = 9 unique opponents, so 5 rematches distributed).

**Why:** Need matchups to determine weekly head-to-head winners.

**Where:**
- New model: `MatchupEntity` (Week, HomeAgentId, AwayAgentId, HomePoints, AwayPoints, IsComplete)
- New service: `IScheduleService` / `ScheduleGenerator` — generates and persists the full 14-week schedule
- New API endpoint: `GET /api/league/schedule`, `GET /api/league/schedule/{week}`
- New MCP tool: `GetWeeklyMatchup(agentId, week)` — so agents can see who they're playing

### 4.3 — Weekly Scoring Engine (LeagueAPI)

**What:** For a given week, look at each agent's *starting* lineup, sum their `WeeklyPlayerPoints` for that week, and write the totals to the matchup record.

**Why:** This is the core game mechanic — determining who wins each week.

**Where:**
- New service: `IWeeklyScoringService` — `ScoreWeek(season, week, templateKey)`
- Reads each agent's starters → looks up `WeeklyPlayerPoints` → sums → writes to `MatchupEntity.HomePoints`/`AwayPoints`
- New API endpoint: `POST /api/league/score/{week}`, `GET /api/league/scoreboard/{week}`
- New MCP tool: `GetWeeklyScoreboard(week)` — agents can see results

### 4.4 — Standings Service (LeagueAPI)

**What:** Compute W/L record, points for, points against, and ranking for each team across all completed weeks.

**Why:** Standings drive playoff seeding and give agents context for decision-making.

**Where:**
- New model: `StandingsEntry` (read model, not persisted — computed from matchups)
- New service: `IStandingsService` — `GetStandings(throughWeek?)`
- New API endpoint: `GET /api/league/standings`
- New MCP tool: `GetStandings()` — agents can check their position

### 4.5 — Waiver Wire System (LeagueAPI)

**What:** After each week, agents can submit waiver claims (add player X, drop player Y). Claims are processed in priority order — successful claim resets that agent's priority to last.

**Why:** This is how agents improve their rosters during the season.

**Where:**
- New model: `WaiverClaimEntity` (AgentId, Week, AddSleeperPlayerId, DropSleeperPlayerId, Priority, Status, ProcessedAtUtc)
- New service: `IWaiverService` — `SubmitClaim(...)`, `ProcessWaiverClaims(week)`
- Processing logic: sort claims by priority → for each claim, check if target player is still available → if yes, execute add/drop and reset priority → if no, skip
- New API endpoint: `POST /api/league/waivers/claim`, `POST /api/league/waivers/process/{week}`, `GET /api/league/waivers/{week}`
- New MCP tool: `SubmitWaiverClaim(agentId, addSleeperPlayerId, dropSleeperPlayerId)`

### 4.6 — Playoff Bracket System (LeagueAPI)

**What:** After Week 14, seed the top 6 teams into a 3-week single-elimination bracket (Weeks 15–17). Seeds 1–2 get a first-round bye. Bracket: Week 15 (seeds 3v6, 4v5), Week 16 (seed 1 vs lower winner, seed 2 vs higher winner), Week 17 (championship).

**Why:** Playoffs are included per Casey's decision. Gives a definitive champion.

**Where:**
- New model: `PlayoffMatchupEntity` (Week, Seed1AgentId, Seed2AgentId, Points, Winner, Round)
- Extend `IScheduleService` with `GeneratePlayoffBracket(standings)` and `AdvancePlayoffRound(week)`
- New API endpoints: `GET /api/league/playoffs`, `POST /api/league/playoffs/advance/{week}`
- New MCP tool: `GetPlayoffBracket()`

### 4.7 — League MCP Tools Bundle (LeagueAPI)

**What:** Register all new MCP tools so agents can interact with the league system.

**Where:**
- New file: `Tools/LeagueTools.cs` — aggregates schedule, scoreboard, standings, waiver, lineup, and playoff tools
- Register in `Program.cs` alongside existing `PlayerCatalogTools`, `RosterTools`, `YahooReadTools`

### 4.8 — Season Runner (AgenticLeague)

**What:** The orchestrator that drives the season week by week. After the draft, it loops through weeks 1–17, executing the weekly cycle for each.

**Weekly cycle:**
1. **Daily roster management** — prompt each agent to manage their roster (set lineup, react to injuries, evaluate free agents). Runs at least once per day during the week window.
2. **Lock rosters** — no more changes once "games start" (conceptual lock)
3. **Score the week** — call the scoring engine with that week's Yahoo data
4. **Post results** — update matchups, standings
5. **Waiver window** — prompt agents to submit waiver claims based on results and available players
6. **Process waivers** — execute claims in priority order
7. **Advance to next week**

After Week 14, transitions to playoff mode (3-week bracket, same daily cycle but with elimination matchups).

**Where:**
- New file: `AgenticLeague/SeasonRunner.cs`
- Called from `Program.cs` after `DraftRunner` completes
- Uses prompts tailored to each phase (lineup setting, waiver decisions)

### 4.9 — Agent Prompts for Season Play (AgenticLeague)

**What:** New prompt templates for each phase of the weekly cycle.

**Where:**
- `Prompts/FantasyAgent.set-lineup.md` — instructions for setting weekly lineup
- `Prompts/FantasyAgent.waiver-claim.md` — instructions for evaluating waiver wire
- Update `FantasyAgent.how-to-play.md` with new tools

### 4.10 — Injury/Status Reactive Prompting (AgenticLeague)

**What:** When a player's status changes (e.g., to OUT, IR, Questionable), check if that player is on an agent's roster and prompt only that agent to react.

**Why:** This is called out specifically in the plan.md — agents should react to breaking news, not just scheduled prompts.

**Where:**
- Could hook into the nightly Sleeper sync — compare before/after status for rostered players
- New service in AgenticLeague: `InjuryAlertService` — detects changes, triggers targeted agent prompts
- Or: a polling check as part of the SeasonRunner loop

---

## Dependency Order

```
4.1 (Lineup Slots)     ─┐
4.2 (Schedule)          ─┤
                         ├→ 4.3 (Scoring) → 4.4 (Standings) → 4.6 (Playoffs)
4.5 (Waivers)           ─┤
                         └→ 4.7 (MCP Tools) → 4.8 (Season Runner) → 4.9 (Prompts)
                                                                    → 4.10 (Injury Alerts)
```

4.1, 4.2, and 4.5 can be built in parallel. 4.3 needs 4.1 + 4.2. 4.6 needs 4.4. Everything feeds into 4.7/4.8.

## Risks and Considerations

- **Token cost**: Each weekly cycle prompts 10 agents multiple times. At ~$0.06/agent/prompt, a 17-week season with daily prompting = potentially $50+. Monitor and optimize.
- **Agent reliability**: Some LLMs may fail to set valid lineups. Need validation + fallback (auto-fill starters if agent fails).
- **Yahoo data availability**: Scoring depends on Yahoo weekly stats being synced for each week. Need to ensure data exists before scoring.
- **Draft round count**: `DraftRunner` is hardcoded to 2 rounds — needs to be bumped to 15 for a real season. This is Step 5 territory but tightly coupled.
