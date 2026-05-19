# Waiver Wire Testing Guide

This document walks through a simple manual test flow for the waiver wire system.

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

## 1. Seed Waiver Priority

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

## 2. Check Available Players

Use this to find a few unowned players to target in waiver claims:

```bash
curl "http://localhost:5039/api/players/available?limit=10"
```

Pick at least one player that multiple agents will try to claim so you can verify priority behavior.

## 3. Update `AgenticLeague/Program.cs` for Agent Submission Testing

For the first agent test, do not use `AddPlayerToRoster`.

Instead, have an agent:

1. Call `GetMyWaiverStatus`
2. Inspect its roster and available players
3. Submit 2-3 prioritized claims with `SubmitWaiverClaims`
4. Report what it submitted

Suggested prompt:

```text
For season 2025 week 1, first call `GetMyWaiverStatus`. If the phase is `waiver_window`, review your roster and available players, then submit 2-3 prioritized waiver claims using `SubmitWaiverClaims`. If you already have pending claims, review them and decide whether to replace them. Report your final submitted claim list and why.
```

Run this for 2-3 different agents.

Important:

- Use overlapping targets across agents
- Make sure each claim includes both an add player and a drop player
- Use the same season and week for all agents

## 4. Optional: Submit Claims Through REST Instead of Agents

If you want to test the backend directly before wiring agent prompts, you can submit claims through REST:

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
        "addSleeperPlayerId": "ADD_PLAYER_ID_1",
        "dropSleeperPlayerId": "DROP_PLAYER_ID_1"
      },
      {
        "claimOrder": 2,
        "addSleeperPlayerId": "ADD_PLAYER_ID_2",
        "dropSleeperPlayerId": "DROP_PLAYER_ID_2"
      }
    ]
  }'
```

Repeat for another agent with at least one overlapping `addSleeperPlayerId`.

## 5. Process Waivers

After claims are submitted, run waiver processing once:

```bash
curl -X POST "http://localhost:5039/api/league/waivers/2025/1/process"
```

This is the admin/league step. Agents should not do this themselves.

## 6. Inspect Results

Check processed claims:

```bash
curl "http://localhost:5039/api/league/waivers/2025/1?agentId=player-05"
```

Check process status:

```bash
curl "http://localhost:5039/api/league/waivers/2025/1/status"
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

## 7. Test Free Agency After Waivers

Once waivers are processed, agents that missed out can add a free agent immediately.

Suggested follow-up prompt:

```text
For season 2025 week 1, call `GetMyWaiverStatus` and review your `MyClaims`. If the phase is `free_agency` and your waiver claims did not improve your team, add one free agent with `AddFreeAgent` and report what you changed.
```

You can also test it directly with REST:

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

## 8. Final Verification Checklist

Confirm all of the following:

- waiver priority seeded correctly
- agents can see `waiver_window` before processing
- agents can submit claim lists
- waiver processing completes successfully
- successful claims update the roster
- new players land on `BN`
- after processing, agents see `free_agency`
- free-agent adds work only after waivers are processed

## Notes

- `AgenticLeague/Program.cs` is best used as an agent driver for submitting claims and reacting to results.
- `LeagueAPI` REST endpoints are still the easiest way to seed priority and process waivers manually.
- For the first test pass, keep it simple: 2-3 agents, 1 week, and a few overlapping claims.
