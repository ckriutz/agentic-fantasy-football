# Player evaluation rubric (start/sit)

Use this when choosing who starts among eligible rostered players for the **current week**.

## Absolute filters (apply first)

Remove from start consideration (put/keep on `BN`) if any apply:

1. **Bye**: `byeWeek == currentWeek`
2. **Unavailable**: `injuryStatus` in `Out`, `IR`, `PUP`, `Suspended` (or equivalent "will not play")
3. **Already scored / locked out of moves**: treat `lockStatus.isLineupMoveLocked` as immovable; if already in a starter slot, that slot is taken

`Doubtful`: default to bench unless research strongly says they will play useful snaps.  
`Questionable` / empty-but-rumored: break ties toward the healthier alternative; Research when it would change start/sit.

Null / empty `injuryStatus` with normal `status` ⇒ treat as healthy unless news says otherwise.

## Ranking signals (eligible players only)

Compare players **at the same decision point** (e.g. two RBs fighting for RB2/FLEX). Use this priority order:

| Priority | Signal | Direction | Notes |
|----------|--------|-----------|-------|
| 1 | Expected to play meaningful snaps this week | Higher better | Injuries, depth chart, coach speak via `SearchWeb` when needed |
| 2 | `projectedFantasyPoints` | Higher better | Best week-oriented scoring proxy when present |
| 3 | `rankAverage` | **Lower** better | FantasyPros consensus rank; prefer over `searchRank` when present |
| 4 | `positionRank` | **Lower** better | Positional rank string (e.g. `QB1` > `QB2`); compare within same position |
| 5 | `tier` | **Lower** better | FantasyPros tier bucket; useful for broad quality bands |
| 6 | `playerOwnedAverage` | Higher better | Global ownership %; higher implies more consensus demand |
| 7 | `searchRank` | **Lower** better | Platform overall rank; ignore absurd sentinels like `9999999` as "unranked" |
| 8 | Recent `weeklyPoints` | Higher / trend up better | Form check; do not overweight one fluke week alone |
| 9 | `depth_chart_order` | Lower better (`1` preferred) | Playing-time stability |
| 10 | `lastSeasonFantasyPoints` | Higher better | Prior production; weaker than current projections/rank |
| 11 | `auctionValue` | Higher better | Soft market-value tie-break |

### Interpreting weak data

- Missing projections ⇒ lean harder on `rankAverage` / `positionRank` / `searchRank`, depth chart, and (if needed) web consensus.
- Missing FantasyPros fields (`rankAverage`, `positionRank`, `tier`, `playerOwnedAverage` null) ⇒ fall back to `searchRank` and projections as before.
- Unranked (`searchRank` null/`9999999`, or empty `rankAverage`) with low projected points ⇒ usually bench vs any normal skill starter.
- Large projection vs last-season gap ⇒ do not auto-trust either; if the start is critical, a quick `SearchWeb` check helps.

## Position fill strategy

### Locked slots first

If a player is lineup-locked in a starter slot, leave them. Fill **other** slots around them. Do not try to "optimize" a locked bad start.

### Positional starters before FLEX

1. Best QB → `QB1`
2. Top two eligible RBs → `RB1`, `RB2` (RB1 = slightly better of the two; labeling is mostly cosmetic)
3. Top two eligible WRs → `WR1`, `WR2`
4. Best TE → `TE1`
5. Best remaining RB/WR/TE → `FLEX1`
6. Best K → `K1`
7. Best DEF → `DEF1`

### FLEX construction

After RB/WR/TE primary slots are filled, choose FLEX as the **single highest-ranked remaining** RB, WR, or TE by the ranking table above.

Do **not**:

- Put QB/K/DEF in FLEX
- Leave FLEX empty while a healthy non-bye skill player sits on the bench
- Force a positional "balance" that starts a worse player

### Kickers and defense

Usually thin benches. Start the rostered K/DEF unless bye/out/locked-to-bench forces a void. Do not over-research K/DEF unless injury/bye creates a hole you cannot fill (lineup skill cannot pick up free agents).

## Close calls

When two players are nearly equal:

1. Prefer the healthier / more certain to play
2. Prefer better projection, then better (lower) `rankAverage` / `positionRank` / `searchRank`
3. Prefer higher recent form if both are healthy
4. Prefer not moving slots if already set correctly (stability / fewer failed locked moves)

Document close calls in the final decision **Why** section.

## Common failure modes

| Mistake | Correct behavior |
|---------|------------------|
| Starting a bye player because rank is elite | Always bench on bye |
| Starting Out player "for upside" | Bench; start next best eligible |
| Blindly trusting `AutoSetLineup` | It ignores injury/bye judgment — verify/fix |
| Moving locked Thursday starter after kickoff | Leave locked; optimize remaining slots only |
| Swapping WR1/WR2 with no ranking change | No-op; leave lineup |
| Empty FLEX while RB3 sits healthy | Promote best remaining flex-eligible player |
