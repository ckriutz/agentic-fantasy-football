
# Waiver Wire Claim Decision

## Purpose

Your job is to evaluate your roster, identify weaknesses, research available players, and make **one** player-acquisition move when it improves your team.
**IMPORTANT**: This is optional. If your research concludes you are happy with your roster, do not make a move.

---

## Steps

### 1. Check the Current League Phase

Call `GetLeagueState` first.

Use the returned `season`, `week`, and `phase` as the source of truth.

There are only two acquisition phases you should act on:

- `waiver_window`
- `free_agency`

If the phase is anything else, do not try to add a player.

Then call `GetMyWaiverStatus` with your agent ID to review:

- **Phase**
- **MyPriority**
- **HasPendingClaims**
- **MyClaims**

---

### 2. Assess Your Roster

Use `ReadAgentBootstrap` to review your current strategy and team notes.

Use `GetMyRoster` to load your roster and identify:
- Players who are injured (`injuryStatus` = Out or IR)
- Players on bye this week
- Positional weaknesses (thin positions, underperforming starters)
- Any player whose value has dropped significantly

---

### 3. Research Available Players

Use `GetAvailablePlayers` filtered by the position(s) you need most. Focus on:
- Players ranked well by `searchRank` (lower = better)
- Players with upside from injury recoveries, role expansions, or favorable matchups

Use `SearchWeb` to research the top candidates:
- Current injury status and recovery timeline
- Depth chart standing and role clarity
- Upcoming schedule and matchup difficulty
- Beat reporter notes and practice reports

---

### 4. Decide Whether to Make a Move

Choose **one** player you want to add.

If your roster is full, also choose the player you will drop.
If your roster has room, you can omit `dropSleeperPlayerId`.

Guidelines:
- Only make the move if the added player is a real upgrade or fills an important need.
- Be strategic about which player to drop — do not drop a healthy starter or a player you may need later.
- Prefer simple, high-confidence moves over speculative churn.

---

### 5. Use the Correct MCP Tool for the Phase

If `phase` is `waiver_window`:

- Call `SubmitWaiverClaimForCurrentWeek`
- Inputs:
  - `agentId`
  - `addSleeperPlayerId`
  - `dropSleeperPlayerId` only if the roster is full

If `phase` is `free_agency`:

- Call `AddFreeAgentForCurrentWeek`
- Inputs:
  - `agentId`
  - `addSleeperPlayerId`
  - `dropSleeperPlayerId` only if the roster is full

Do **not** call the older explicit season/week acquisition tools unless you were explicitly instructed to do admin or debug work.

---

### 6. After the Move

After submitting a waiver claim or adding a free agent:

1. Review the tool result carefully.
2. If a player was successfully added:
   - The new player is on your bench (`BN`).
   - Use `SetPlayerSlot` or `AutoSetLineup` if your starting lineup should change.
3. Update your bootstrap file with the move and your reasoning.

---

## What to Avoid

- Do not skip `GetLeagueState` — always check the current phase first.
- Do not use the wrong acquisition tool for the phase.
- Do not drop an injured player just because they are injured — if they have strong projected value when healthy, keep them.
- Do not make a move unless the player you are adding is a genuine upgrade over the player you are dropping.
- Do not provide a `dropSleeperPlayerId` unless you truly want to cut that player.
- Do not forget to update your starting lineup after a successful add — a player on `BN` earns zero points.
