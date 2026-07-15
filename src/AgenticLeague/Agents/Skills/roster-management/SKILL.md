---
name: roster-management
description: Set and optimize a full fantasy football starting lineup from the current roster. Use for complete roster management, empty lineups, all players on bench, assigning starter slots, start/sit, slot placement (QB1 RB1 RB2 WR1 WR2 TE1 FLEX1 K1 DEF1 BN), bye weeks, injury checks, locked games, or set/manage lineup. Do not use for drafting, waiver claims, free-agent adds, or trades.
metadata:
  author: agentic-league
  version: "1.1"
  domain: fantasy-football
---

# Roster Management

Set the best **starting lineup from players already on the roster**. Goal: maximize points for the current NFL week’s remaining games.

**Success condition:** every starter slot is filled when an eligible rostered player exists for that slot. Leaving all (or most) players on `BN` after a lineup task is a failure unless locks or empty position pools make a fill impossible.

This skill is lineups only. Do **not** add, drop, claim, or trade players here.

## When to use

- Complete roster management / “set my lineup”
- Empty lineup or **all players on bench**
- Daily or weekly start/sit and flex decisions
- Bye week or injury coverage on the current roster
- Pre-game lock checks (Thursday / Sunday / Monday)
- Narrow asks like “bench one player” when the starting lineup is incomplete — **set the full lineup first**

## Slot map

Valid starter slots (exact names):

| Slot | Eligible positions |
|------|--------------------|
| `QB1` | QB |
| `RB1`, `RB2` | RB |
| `WR1`, `WR2` | WR |
| `TE1` | TE |
| `FLEX1` | RB, WR, or TE |
| `K1` | K |
| `DEF1` | DEF |
| `BN` | any (bench; scores 0) |

Rules:

- Only **starters** score points.
- A player may occupy only one slot.
- Placing a player into an occupied starter slot benches the previous occupant.
- Never invent slot names.
- A complete lineup uses all of: `QB1`, `RB1`, `RB2`, `WR1`, `WR2`, `TE1`, `FLEX1`, `K1`, `DEF1`.

## Tools

Use league tools (MCP) plus research tools already available to you:

| Tool | Purpose |
|------|---------|
| `GetLeagueState` | Current `season`, `week`, `phase` |
| `GetMyRoster` | Full roster with slots, byes, injuries, projections, locks |
| `SetPlayerSlot` | Move a player to `QB1`…`DEF1` or `BN` — **primary way to set the lineup** |
| `SearchWeb` | Confirm questionable/doubtful injuries, start/sit news |
| `ReadAgentBootstrap` / `WriteAgentBootstrap` | Strategy context; optional short notes after changes |
| `AutoSetLineup` | **Do not use for normal runs** |

Primary tools for this skill: `GetLeagueState` → `GetMyRoster` → repeated `SetPlayerSlot` (+ `SearchWeb` when status is uncertain).

### AutoSetLineup ban

- **Do not call `AutoSetLineup`** for normal roster management.
- It sorts by `searchRank` only and ignores bye / injury judgment.
- Only if many individual `SetPlayerSlot` calls fail for non-judgment reasons may you consider it — and you must immediately re-read the roster and correct byes, outs, and empty slots with `SetPlayerSlot`.

Preferred decision writing:

1. If a `LogDecision` / decision-logging tool is available, call it (see [decision log format](references/decision-log-format.md)).
2. Always end with the **required decision summary** below so the host can persist Type / Action / Reasoning.

## Workflow

Do these steps in order.

### 1. Establish week context

1. Call `GetLeagueState`.
2. Note `week` (current NFL week) and `phase`.
3. Know your `agentId` (from identity / task). Always pass it to roster tools.

### 2. Load roster

Call `GetMyRoster(agentId)`.

For each player, inspect:

- `slotType`, `isStarter`, `position`
- `byeWeek`
- `injuryStatus`, `injury_body_part`
- `projectedFantasyPoints`, `lastSeasonFantasyPoints`
- `rankAverage` (lower better; FantasyPros consensus rank)
- `positionRank` (e.g. `QB1` better than `QB2`)
- `tier` (lower better)
- `playerOwnedAverage` (higher better ownership %)
- `searchRank` (lower better; `9999999` / missing ≈ unranked; fallback if FantasyPros ranks absent)
- `depth_chart_order` (1 ≈ starter)
- `weeklyPoints` (recent form)
- `lockStatus` — especially `isLineupMoveLocked`, `hasPlayedThisWeek`, `lineupMoveLockReason`

### 2b. Detect empty or incomplete lineup (do this before fine-tuned start/sit)

Count how many of the nine starter slots are currently occupied: `QB1`, `RB1`, `RB2`, `WR1`, `WR2`, `TE1`, `FLEX1`, `K1`, `DEF1`.

If **any** of those slots is empty (common case: everyone is on `BN`):

1. You are in **full set** mode, not “bench one player” mode.
2. Build a complete target lineup using the rubric.
3. Apply `SetPlayerSlot` until every fillable starter slot has a player.
4. Only after the nine slots are filled as well as eligibility allows, apply narrower user requests (e.g. specific bench preferences).

Never stop at “I benched a risky player” while starter slots remain empty.

### 3. Classify eligibility for this week

For each rostered player:

| Condition | Start eligibility |
|-----------|-------------------|
| `byeWeek == current week` | **Ineligible** — must be `BN` |
| `injuryStatus` is `Out`, `IR`, `PUP`, `Suspended`, or clearly not playing | **Ineligible** — must be `BN` |
| `injuryStatus` is `Doubtful` | **Strong bench bias** — start only with solid evidence they will play meaningful snaps |
| `injuryStatus` is `Questionable` / uncertain | Research with `SearchWeb` before starting over a healthy alternative |
| `lockStatus.isLineupMoveLocked == true` | **Do not move** this player |
| Healthy, not on bye, unlocked | Eligible — rank with the rubric |

Never leave a bye or Out player in a starter slot if an unlocked eligible replacement exists.

### 4. Research only when it changes a decision

Use `SearchWeb` when a likely starter is `Questionable` / `Doubtful`, data looks stale, or a flex call is very close.

Skip web research for clearly healthy players with no flags.

Read [player evaluation rubric](references/player-evaluation-rubric.md) for ranking signals, injury hierarchy, and FLEX construction.

### 5. Build the target lineup

Fill slots in this order so flex leftovers stay available:

1. `QB1`
2. `RB1`, `RB2`
3. `WR1`, `WR2`
4. `TE1`
5. `FLEX1` — best remaining eligible RB/WR/TE
6. `K1`
7. `DEF1`
8. Everyone else → `BN`

Within each position bucket:

1. Drop ineligible players (bye / out).
2. Rank remaining with the [player evaluation rubric](references/player-evaluation-rubric.md).
3. Choose the best unlocked eligible player for each open starter slot.
4. Prefer keeping a **locked starter** as-is. Build around locked players.

If fewer than needed eligible players exist for a slot, document the empty slot in the summary. That is the only acceptable empty starter.

### 6. Apply moves with `SetPlayerSlot`

Compare target lineup vs current `slotType`.

- Empty incomplete lineup → move **every** player who needs a starter slot (many calls is normal and expected).
- Already optimal → **make no changes**.
- Otherwise call `SetPlayerSlot(agentId, sleeperPlayerId, slotType)` for each player that must move.
- Minimize useless churn of equal players (e.g. only swap WR1/WR2 if needed).
- If a call fails because a player is locked, leave them, fill other slots, and note it.
- After applying moves, optionally call `GetMyRoster` again and verify all nine starter slots are filled when players allow.

### 7. No-change is a valid outcome

Only when:

- All nine starter slots that can be filled **are** filled, **and**
- Every starter is already the best valid choice (byes/outs benched)

Then:

- Do not call `SetPlayerSlot`
- Explicitly conclude **no lineup changes were needed**
- Still write the decision summary

If starters are empty, `no_change` is **not** valid.

### 8. Record the decision (required)

End with this exact structure (fill all sections):

```markdown
## Lineup decision (Week {week})
**Loaded skill:** roster-management
**Outcome:** changed | no_change
**Action:** <one-line action for the decisions table>
**Starting lineup:**
- QB1: ...
- RB1: ...
- RB2: ...
- WR1: ...
- WR2: ...
- TE1: ...
- FLEX1: ...
- K1: ...
- DEF1: ...
**Changes:**
- Player (`sleeperId`): OLD → NEW — reason
- (or "None")
**Notable start/sit calls:**
- ...
**Why:**
- ...
**Open risks:**
- ...
```

Use Type `start_sit` when logging (or host Type if provided).  
Action examples:

- `start_sit: week 3; filled full lineup from all-BN`
- `no_change: week 3; lineup already optimal`
- `start_sit: week 7; CMC→BN (bye); Ford→RB1`

Optional: after meaningful lineup changes, update `bootstrap.md` with one concise dated note under `## Decision Log` describing the action and rationale. Update `## Strategy Updates` only for lasting strategic implications. Preserve the existing game plan; do not replace it with the newest note.

## Hard constraints

- Do not add/drop/waive/trade in this skill.
- Do not move locked players (`lockStatus.isLineupMoveLocked`).
- Never start a player on their bye week.
- Prefer a healthy backup over a confirmed-out starter even if the backup has a worse `searchRank`.
- Do not use `AutoSetLineup` on normal runs.
- Complete fill of starter slots is mandatory when eligible players exist.
- Respond with the full decision summary every time.

## Quick checklist

- [ ] Current week known from `GetLeagueState`
- [ ] Roster loaded via `GetMyRoster`
- [ ] Empty/incomplete lineup detected → full set mode
- [ ] Bye players benched
- [ ] Out/IR (and researched Q/D) handled
- [ ] Locked players untouched
- [ ] All fillable starter slots occupied (QB1…DEF1)
- [ ] Position slots legal; FLEX is RB/WR/TE
- [ ] Used `SetPlayerSlot` (not `AutoSetLineup`) unless exceptional failure
- [ ] Decision summary includes starting lineup list

See [examples](references/examples.md) for empty-lineup, bye, injury, flex, lock, and no-change patterns.
