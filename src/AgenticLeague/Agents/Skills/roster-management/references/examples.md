# Roster management examples

Illustrative patterns—not live data. Follow the same structure with real tool results.

## Example 0 — All players on bench (full set)

**Context:** Week 3. Every rostered player has `slotType=BN`. User said “manage my roster.”

**Required behavior:**

1. `load_skill` → roster-management  
2. Detect incomplete lineup (0/9 starters filled) → full set mode  
3. Rank eligible players; call `SetPlayerSlot` for **all nine** starter slots  
4. End with starting lineup listed  

**Wrong:** Call `AutoSetLineup` then only adjust one TE to BN while starters stay empty.  
**Wrong:** “Benched Kittle successfully” without filling QB1…DEF1.

**Action line:**

```text
start_sit: week 3; filled full lineup from all-BN
```

## Example A — Bye week swap

**Context:** Week 9. RB1 is on bye. Healthy RB3 on bench who outranks other options.

```text
GetLeagueState → week=9
GetMyRoster →
  CMC RB slotType=RB1 byeWeek=9 injuryStatus=null
  Jacobs RB slotType=RB2 byeWeek=12
  Ford RB slotType=BN byeWeek=5
```

**Target:** Bench CMC; promote Ford (or best non-bye RB) into RB1/RB2 as ranking dictates.

```text
SetPlayerSlot(agentId, cmcId, "BN")
SetPlayerSlot(agentId, fordId, "RB1")  # if Ford ranks above Jacobs, swap accordingly
```

**Action line:**

```text
start_sit: week 9; CMC→BN (bye); Ford→RB1
```

## Example B — Injury forces flex change

**Context:** Week 4. WR1 listed Out. TE2 has better rank than WR3 for FLEX, WR2 stays.

```text
injuryStatus Out on Waddle (WR1)
Healthy bench: Hopkins (WR), Likely (TE)
```

**Moves:**

```text
SetPlayerSlot(..., waddleId, "BN")
SetPlayerSlot(..., hopkinsId, "WR1")   # if he is next WR
# FLEX already optimal → no flex move
```

**Action line:**

```text
start_sit: week 4; Waddle→BN (Out); Hopkins→WR1
```

## Example C — Questionable starter needs research

**Context:** RB2 is Questionable. Bench RB has much worse rank.

```text
SearchWeb "Alvin Kamara injury status week 6 expected to play"
→ Reports indicate expected to play, limited practice but active likely
```

**Decision:** Keep Questionable star starting; note risk. No change if already RB2.

**Action line:**

```text
no_change: week 6; Kamara Q but research says play; better than bench RB
```

## Example D — Locked Thursday game

**Context:** Sunday run. Thursday RB already locked in at RB1 and played.

```text
lockStatus.isLineupMoveLocked=true on that RB
Another RB on BN is higher rank but irrelevant for the locked slot
```

**Decision:** Do not call SetPlayerSlot on the locked player. Optimize WR/TE/FLEX/others only.

**Action line:**

```text
start_sit: week 2; partial; left locked RB at RB1; Evans→FLEX1; Merritt→BN
```

## Example E — Already optimal (no change)

**Context:** All primary starters healthy, no byes among starters, bench does not beat any starter on rubric, no locks blocking fixes.

**Moves:** none.

**Final summary:**

```markdown
## Lineup decision (Week 3)
**Outcome:** no_change
**Action:** no_change: week 3; lineup already optimal
**Changes:**
- None
**Notable start/sit calls:**
- FLEX stays Lamb over bench RB — higher projection and better searchRank
**Why:**
- No byes or outs among current starters
- Bench alternatives worse on projectedFantasyPoints and searchRank
**Open risks:**
- None
```

## Example F — Minimal position card (mental model)

After evaluation you might hold a internal grid like:

```text
QB1  → Player Q
RB1  → Player R1
RB2  → Player R2
WR1  → Player W1
WR2  → Player W2
TE1  → Player T
FLEX1→ Player F
K1   → Player K
DEF1 → Team D
BN   → everyone else
```

Only emit `SetPlayerSlot` for cells that differ from `GetMyRoster` **and** are unlocked.
