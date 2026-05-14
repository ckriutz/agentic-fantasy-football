# Fantasy Football Agent Guide

## Purpose

This guide defines how an AI agent should use available tools and interpret player data to make fantasy football decisions. Use it as a reference for roster management, player evaluation, and lineup setting.

---

## Available Tools

### `SearchPlayers`
**Use when:** Looking up a specific player by name, team, position, or bye week.

Returns: player stats, ownership percentage, and availability status.

### `GetAvailablePlayers`
**Use when:** Finding free agents to add to a roster.

Returns: all unowned players with stats and availability metadata.

### `GetMyRoster`
**Use when:** Viewing your current roster, including starters and bench players.

Returns: all players currently on your roster with stats and availability metadata.

### `SearchWeb`
**Use when**: You need to look up players, teams, positions, and fantasy football guidance. Very helpful for researching players. You can do this at any time.

Returns: Research data.

### `SetPlayerSlot`
**Use when**: You need to place a player in a position on a starting slot. The valid slot values are: **QB1, RB1, RB2, WR1, WR2, TE1, FLEX1, K1, DEF1, BN**. You must use these exact slot names (including the number suffix).

### `GetMyWaiverStatus`
**Use when**: You need to understand the current waiver situation for a given week. Returns everything in one call: what phase the week is in (`waiver_window` or `free_agency`), your waiver priority position, whether you already have pending claims, and all your claim details with results.

Returns: phase, your priority, pending claim status, and full claim list.

### `SubmitWaiverClaims`
**Use when**: The phase is `waiver_window` and you want to claim a free agent. Submit a prioritized list — only one claim will succeed per waiver period. Replaces any previous pending claims.

Returns: your submitted claim list with statuses.

### `AddFreeAgent`
**Use when**: The phase is `free_agency` (waivers have been processed) and you want to immediately add an unclaimed player. Call `GetMyWaiverStatus` first to confirm the phase.

Returns: confirmation of the add/drop.

---

## Player Data Reference

| Field | Meaning | How to use |
|---|---|---|
| `search_rank` | Overall player ranking; lower = better. `9999999` = unranked/insufficient data | Primary sort key when comparing players |
| `auctionValue` | Estimated auction draft value; higher = more desirable | Use to gauge relative value in trade/add decisions |
| `projectedFantasyPoints` | Expected points this season | Prefer players with higher projections |
| `lastSeasonFantasyPoints` | Actual points scored last season | Use to validate projections; flag large discrepancies |
| `averageDraftPosition` | Average pick position in drafts; lower = higher demand | Use to assess market consensus on a player |
| `byeWeek` | Week the player's team has no game | **Never start a player on their bye week** |
| `depth_chart_order` | Player's position on the depth chart; 1 = starter | Use to determine likely playing time |
| `injuryStatus` | Injury status (e.g., null, "Questionable", "Out", "IR") | Bench players who are "Out"; monitor "Questionable" closely. IR mean the player will be out for an extended period of time. Null values mean no injury concerns currently reported |

---

## Roster Management Rules

### Starting Lineup
- Only players in the **starting lineup** earn points each week.
- Players on the **bench** earn 0 points regardless of their performance.
- A player on **bye** earns 0 points — remove them from the starting lineup for that week.
- A player in the starting lineup who **does not play** (injury, coach's decision) earns 0 points.

### Decision Logic

**When setting a lineup:**
1. Check each starter's `byeWeek` — bench any player on bye.
2. Check injury/availability status using the value `injuryStatus` — if a players is questionable you may not need to bench the player, but check constantly for updates on the player's status. If a player is out, bench them.
3. Compare `projectedFantasyPoints` among eligible players at each position — start the highest projection.

**When a roster spot is weak:**
1. Call `GetAvailablePlayers` filtered by the needed position.
2. Sort results by `search_rank` (ascending) or `projectedFantasyPoints` (descending).
3. If the free agent outperforms the current roster player, recommend the add/drop.

**When evaluating a trade:**
1. Use `SearchPlayers` to pull stats on all players involved.
2. Compare `projectedFantasyPoints`, `lastSeasonFantasyPoints`, and `auctionValue` on both sides.
3. Factor in `byeWeek` conflicts for the current roster.

### Goal
Maximize total points scored each week by fielding the best available starting lineup.

## Roster Management Tools
When you need to add/drop players or set your lineup, use the following tools:
- `AddPlayerToRoster`: Add a player to your roster from free agency.
- `RemovePlayerFromRoster`: Remove a player from your roster.
- `GetMyRoster`: View your current roster, including starters and bench players.

---

## Waiver Wire

### How It Works

- **Waiver window**: After each week ends, there is a waiver period where you can submit claims for players not on any roster.
- **Priority**: Claims are processed in priority order — lower priority number = processed first. Priority is rolling: a successful claim moves you to the **end** of the queue.
- **Claim list**: You submit a *prioritized list* of claims, not just one. Only **one claim will succeed** per waiver period. Your list is tried in `ClaimOrder` sequence until one succeeds or all fail.
- **Required drop**: Every claim must include a player to drop. You cannot add without dropping.
- **Free agency**: After waivers are processed, unclaimed players become free agents. Use `AddFreeAgent` to pick them up immediately — no waiting for the next waiver run.

### Decision Process for Waiver Claims

1. Call `GetMyWaiverStatus` with your agent ID, season, and week. This tells you:
   - **Phase**: `waiver_window` means you can submit claims. `free_agency` means waivers are done — use `AddFreeAgent` instead.
   - **Your priority**: Lower number = processed first. If your priority is poor, submit multiple fallback claims.
   - **Pending claims**: Whether you already have claims submitted for this week (you can revise by resubmitting).
2. Call `GetMyRoster` to identify weaknesses (injuries, bye weeks, underperformers).
3. Call `GetAvailablePlayers` filtered by the needed position to see who is unclaimed.
4. Use `SearchWeb` to research available players — injury recoveries, depth chart changes, upcoming matchups.
5. Build a prioritized claim list:
   - `ClaimOrder 1`: Your top target (best player, best fit for your team).
   - `ClaimOrder 2+`: Fallback options in case your top target is claimed by a higher-priority agent.
6. Call `SubmitWaiverClaims` with your full list.

### After Waivers Are Processed

1. Call `GetMyWaiverStatus` — check the `Phase` field and review your `MyClaims` for results.
2. If a claim succeeded: the new player is on your bench (`BN`). Use `SetPlayerSlot` or `AutoSetLineup` to update your starting lineup if needed.
3. If all your claims failed and the phase is `free_agency`: use `AddFreeAgent` to pick up an unclaimed player immediately.
4. Update your bootstrap file with any roster changes and notes on your waiver decision.

### Rules
- **Never** use `AddFreeAgent` when the phase is `waiver_window` — it will fail. Always call `GetMyWaiverStatus` first.
- A newly added player always lands on `BN`. Manually move them to a starter slot if they should be starting.
- If you drop a starter, their slot becomes empty. Use `SetPlayerSlot` or `AutoSetLineup` to fill it.

When conducting player research, use the following tools:
- `SearchWeb`: Research current player news, injuries, depth chart changes, matchup context, and rankings. When using this tool, it may help to search for players that you're considering adding to your roster, as well as players currently on your roster to stay up to date on their status and outlook for the season. Also searching for players in certain positions can help you identify players.
- `GetPlayerBySleeperId`: Look up a specific player by Sleeper player ID to get their stats, ownership percentage, and availability status.

Reccomended that any research notes and thoughts be added to your bootstrap file to keep track of your evolving strategy and team information. Update and change as you draft players, and throughout the leauge.
