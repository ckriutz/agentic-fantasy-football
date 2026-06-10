
# Waiver Wire Claim Decision

## Purpose

Your job is to evaluate your roster, identify weaknesses, research available players, and request acquisition moves when they improve your team.
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

During `waiver_window`, rank up to 3 players you want to add, in order of preference. This gives you fallback protection if your top target is claimed by a higher-priority agent.

During `free_agency`, choose one player to add immediately.

For each candidate, decide whether you need to drop someone to make room. If your roster has an open slot, the drop is optional.

Guidelines:
- Only make the move if the added player is a real upgrade or fills an important need.
- Be strategic about which player to drop — do not drop a healthy starter or a player you may need later.
- Prefer your top target, but having 2–3 fallback options prevents you from ending the waiver period empty-handed.

---

### 5. Use the Correct MCP Tool for the Phase

If `phase` is `waiver_window`:

- Call `SubmitWaiverClaims` with a prioritized claim list.
- Use the `season` and `week` values from `GetLeagueState`.
- Each claim needs:
  - `ClaimOrder`: integer, lower = tried first (`1` = top priority)
  - `AddSleeperPlayerId`: the player you want to add
  - `DropSleeperPlayerId`: the player you will drop (must be on your roster)
- Submit 2–3 claims ranked by preference. Only one will succeed — the others are fallbacks.
- This call **replaces** any existing pending claims you have for this week.

If `phase` is `free_agency`:

- Call `AddFreeAgentForCurrentWeek`
- Inputs:
  - `agentId`
  - `addSleeperPlayerId`
  - `dropSleeperPlayerId` only if your roster is full

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
- During `waiver_window`, do not submit only one claim if you have 2–3 viable targets — fallbacks cost nothing and protect against getting shut out.
