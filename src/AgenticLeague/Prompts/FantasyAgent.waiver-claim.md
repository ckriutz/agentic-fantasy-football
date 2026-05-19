# Waiver Wire Claim Decision

## Purpose

The weekly waiver window is open. Your job is to evaluate your roster, identify weaknesses, research available players, and submit a prioritized list of waiver claims. Only one claim will succeed — make your list count.
**IMPORTANT**: This is an optional activity. If your research concludes you are happy with your roster, there is no reason to submit a waver claim.

---

## Steps

### 1. Check Your Waiver Status

Use `GetMyWaiverStatus` with your agent ID, season, and week. This tells you everything you need:

- **Phase**: If `free_agency`, waivers are already done — skip to step 6 and use `AddFreeAgent` instead.
- **MyPriority**: Your position in the waiver queue (lower = better).
- **HasPendingClaims**: Whether you already submitted claims this week.
- **MyClaims**: Your existing claims and their statuses.

If the phase is `waiver_window`, continue to step 2.

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

### 4. Build Your Claim List

Submit a prioritized claim list using `SubmitWaiverClaims`.

Each claim needs:
- `ClaimOrder`: Integer, lower = tried first (1 = top priority)
- `AddSleeperPlayerId`: The player you want to add
- `DropSleeperPlayerId`: The player you will give up (must currently be on your roster)

Guidelines:
- Your `ClaimOrder 1` should be the player who most improves your team.
- If two players fill the same need, put the better one at order 1 and the backup at order 2.
- Be strategic about which player to drop — do not drop a healthy starter or a player you may need.
- You can list 2–5 claims. More fallbacks = better insurance against being outbid.

---

### 5. Submit and Confirm

Call `SubmitWaiverClaims` with `agentId`, `season`, `week`, and your claim list.

This replaces any previous pending claims for this agent and week. You can resubmit before waivers are processed to update your list.

---

### 6. After Waivers Are Processed

Call `GetMyWaiverStatus` again — if the phase is now `free_agency`:

1. Review your `MyClaims` results.
2. If a claim succeeded:
   - The new player is on your bench (`BN`).
   - Use `SetPlayerSlot` or `AutoSetLineup` to update your starting lineup.
3. If all claims failed:
   - Use `AddFreeAgent` to immediately pick up an unclaimed player — no priority required.
4. Update your bootstrap file with the roster change and your reasoning.

---

## What to Avoid

- Do not drop an injured player just because they are injured — if they have strong projected value when healthy, keep them.
- Do not make a claim unless the player you are adding is a genuine upgrade over the player you are dropping.
- Do not forget to update your starting lineup after a successful claim — a player on `BN` earns zero points.
