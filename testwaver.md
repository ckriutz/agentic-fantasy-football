# Waiver Wire Testing Guide

This document walks through a manual test flow for the upgraded waiver/free-agency system.

The expected upgraded flow is:

1. League state stores the current season, week, and phase.
2. Agents call `GetLeagueState` and `GetMyWaiverStatus` instead of relying on prompt text for the week.
3. Agents use one tool, `RequestPlayerAcquisitions`, for add/drop requests.
4. The backend decides whether the request becomes waiver claims or an immediate free-agent add based on league state.

## Prerequisites

1. Make sure Postgres is running.
2. Make sure `DBConnectionString` points to your real database.
3. From `src/LeagueAPI`, apply migrations:

```bash
dotnet ef database update
```

4. Start `LeagueAPI`.
5. Start `AgenticLeague` if you want to drive agent behavior from `src/AgenticLeague/Program.cs`.

## Test Values

Use these example values for the first test run:

- Season: `2025`
- Week: `1`
- Base URL: `http://localhost:5039`

If your API runs on a different port, replace `5039` in the commands below.

## 1. Set League State to Waiver Window

Set the current league state before asking any agent to make a move.

```bash
curl -X PUT "http://localhost:5039/api/league/state" \
  -H "Content-Type: application/json" \
  -d '{
    "season": 2025,
    "week": 1,
    "phase": "waiver_window",
    "updatedBy": "manual-test"
  }'
```

Verify the league state:

```bash
curl "http://localhost:5039/api/league/state"
```

Expected result:

- `season` is `2025`
- `week` is `1`
- `phase` is `waiver_window`

## 2. Seed Waiver Priority

Seed the waiver order using the finished draft order.

```bash
curl -X POST "http://localhost:5039/api/league/waivers/priority/seed" \
  -H "Content-Type: application/json" \
  -d '{
    "draftOrder": [
      "player-08",
      "player-10",
      "player-05",
      "player-04",
      "player-07",
      "player-02",
      "player-09",
      "player-03",
      "player-06",
      "player-01"
    ]
  }'
```

If you need to reset and reseed:

```bash
curl -X POST "http://localhost:5039/api/league/waivers/priority/seed?force=true" \
  -H "Content-Type: application/json" \
  -d '{
    "draftOrder": [
      "player-08",
      "player-10",
      "player-05",
      "player-04",
      "player-07",
      "player-02",
      "player-09",
      "player-03",
      "player-06",
      "player-01"
    ]
  }'
```

Verify the priority:

```bash
curl "http://localhost:5039/api/league/waivers/priority"
```

Expected result:

- `player-01` should have priority `1`
- the seeded order should be the reverse of the draft order

## 3. Check Available Players

Use this to find a few unowned players to target in acquisition requests:

```bash
curl "http://localhost:5039/api/players/available?limit=10"
```

Pick at least one player that multiple agents will try to claim so you can verify priority behavior.

## 4. Agent Submission Testing

For the agent test, do not put season/week in the prompt and do not use `AddPlayerToRoster`.

Instead, have an agent:

1. Call `GetLeagueState`
2. Call `GetMyWaiverStatus`
3. Inspect its roster and available players
4. Submit 2-3 prioritized moves with `RequestPlayerAcquisitions`
5. Report what it requested and why

Suggested prompt:

```text
Review the current league state, then review your waiver status, roster, and available players. If a player acquisition would clearly improve your team, use `RequestPlayerAcquisitions` with 2-3 prioritized add/drop moves. Report the final moves you requested and why. Do not use `AddPlayerToRoster`, `SubmitWaiverClaims`, or `AddFreeAgent` directly.
```

Run this for 2-3 different agents.

Important:

- Use overlapping targets across agents.
- Make sure each requested move includes both an add player and a drop player.
- Do not hardcode season/week in the prompt.

Expected result while league state is `waiver_window`:

- `RequestPlayerAcquisitions` creates waiver claims for the current league state season/week.
- No roster changes happen immediately.
- The tool result mode is something like `waiver_claims_submitted`.

## 5. Optional: Submit Acquisition Requests Through REST Instead of Agents

If you want to test the backend directly before wiring agent prompts, use the single acquisition endpoint:

```bash
curl -X POST "http://localhost:5039/api/league/player-acquisitions" \
  -H "Content-Type: application/json" \
  -d '{
    "agentId": "player-05",
    "moves": [
      {
        "order": 1,
        "addSleeperPlayerId": "ADD_PLAYER_ID_1",
        "dropSleeperPlayerId": "DROP_PLAYER_ID_1"
      },
      {
        "order": 2,
        "addSleeperPlayerId": "ADD_PLAYER_ID_2",
        "dropSleeperPlayerId": "DROP_PLAYER_ID_2"
      }
    ]
  }'
```

Repeat for another agent with at least one overlapping `addSleeperPlayerId`.

## 6. Verify Claims Were Created

Check submitted claims:

```bash
curl "http://localhost:5039/api/league/waivers/2025/1?agentId=player-05"
```

Expected result:

- claims exist for `player-05`
- claim statuses are `Pending`
- `season` and `week` match current league state

## 7. Process Waivers

After claims are submitted, run waiver processing once:

```bash
curl -X POST "http://localhost:5039/api/league/waivers/2025/1/process"
```

This is the admin/league step. Agents should not do this themselves.

Verify processing status:

```bash
curl "http://localhost:5039/api/league/waivers/2025/1/status"
```

Verify league state advanced:

```bash
curl "http://localhost:5039/api/league/state"
```

Expected result:

- waiver process status has `hasBeenProcessed = true`
- league state phase is now `free_agency`
- league state season/week are still `2025` and `1`

## 8. Inspect Waiver Results

Check processed claims:

```bash
curl "http://localhost:5039/api/league/waivers/2025/1?agentId=player-05"
```

Check updated priority:

```bash
curl "http://localhost:5039/api/league/waivers/priority"
```

Verify:

- only one claim succeeded per agent
- overlapping claims respected waiver priority
- successful agents moved to the end of the queue
- lower-priority fallback claims were marked appropriately
- successful adds landed on `BN`

## 9. Test Free Agency With the Same Acquisition Tool

Once league state is `free_agency`, agents should still use `RequestPlayerAcquisitions`. The backend should immediately execute the first valid add/drop instead of creating new waiver claims.

Suggested follow-up prompt:

```text
Review the current league state and your waiver results. If the phase is `free_agency` and your team can still improve, use `RequestPlayerAcquisitions` with prioritized add/drop moves. Report whether an immediate free-agent move was made. Do not use `AddFreeAgent` directly.
```

You can also test it directly with REST:

```bash
curl -X POST "http://localhost:5039/api/league/player-acquisitions" \
  -H "Content-Type: application/json" \
  -d '{
    "agentId": "player-05",
    "moves": [
      {
        "order": 1,
        "addSleeperPlayerId": "ADD_PLAYER_ID",
        "dropSleeperPlayerId": "DROP_PLAYER_ID"
      }
    ]
  }'
```

Expected result:

- tool result mode is something like `free_agent_added`
- one roster add/drop happens immediately
- no new waiver claim is created for player-05
- added player lands on `BN`

## 10. Verify Wrong-Phase Guardrails

After league state is `free_agency`, direct waiver claim submission should fail.

```bash
curl -X POST "http://localhost:5039/api/league/waivers/2025/1/claims" \
  -H "Content-Type: application/json" \
  -d '{
    "agentId": "player-05",
    "season": 2025,
    "week": 1,
    "claims": [
      {
        "claimOrder": 1,
        "addSleeperPlayerId": "ADD_PLAYER_ID",
        "dropSleeperPlayerId": "DROP_PLAYER_ID"
      }
    ]
  }'
```

Expected result:

- request is rejected
- error explains that the current phase is `free_agency`
- error points agents toward `RequestPlayerAcquisitions`

Set league state back to `waiver_window` and verify direct free-agent adds fail:

```bash
curl -X PUT "http://localhost:5039/api/league/state" \
  -H "Content-Type: application/json" \
  -d '{
    "season": 2025,
    "week": 1,
    "phase": "waiver_window",
    "updatedBy": "manual-test"
  }'
```

```bash
curl -X POST "http://localhost:5039/api/league/free-agents/2025/1/add" \
  -H "Content-Type: application/json" \
  -d '{
    "agentId": "player-05",
    "season": 2025,
    "week": 1,
    "addSleeperPlayerId": "ADD_PLAYER_ID",
    "dropSleeperPlayerId": "DROP_PLAYER_ID"
  }'
```

Expected result:

- request is rejected
- error explains that free-agent adds only work during `free_agency`

## 11. Final Verification Checklist

Confirm all of the following:

- league state can be set and read
- agents can discover current season/week/phase through `GetLeagueState`
- waiver priority seeded correctly
- agents can see `waiver_window` before processing
- `RequestPlayerAcquisitions` creates waiver claims during `waiver_window`
- waiver processing completes successfully
- successful claims update the roster
- new players land on `BN`
- waiver processing advances league state to `free_agency`
- agents can see `free_agency` after processing
- `RequestPlayerAcquisitions` performs immediate add/drop during `free_agency`
- direct wrong-phase low-level tools are rejected

## Notes

- `AgenticLeague/Program.cs` is best used as an agent driver for testing whether agents follow the tool flow.
- `LeagueAPI` REST endpoints are still the easiest way to set league state, seed priority, and process waivers manually.
- For the first test pass, keep it simple: 2-3 agents, 1 week, and a few overlapping requested moves.
