# Decision log format (roster management)

The league stores agent decisions in the **decisions** table via LeagueAPI (`POST /api/decisions`) or host-side `DecisionLogger`. Every roster-management run must produce data that maps cleanly into that table—even when nothing changed.

## Fields

| Field | Required | Guidance for this skill |
|-------|----------|-------------------------|
| `AgentId` | yes | Your agent id (e.g. `player-01`) |
| `Week` | yes | NFL week from `GetLeagueState` |
| `Type` | yes | Prefer `start_sit`. Host may also use descriptive types like `Set Lineup for Sunday Games`; match the invoker if they specify a Type |
| `Action` | yes | Short factual outcome (see templates). Keep under ~200 chars when possible |
| `Reasoning` | yes | Full explanation: what you checked, who sat/start, bye/injury notes, why no_change if applicable |
| Token counts | optional | Host fills when logging from `AgentResponse.Usage` |

## Action templates

### Changes applied

```text
start_sit: week {N}; set {PlayerA}→{SLOT}; {PlayerB}→BN ({reason}); {PlayerC}→FLEX1
```

Examples:

```text
start_sit: week 4; CMC→RB1; Hall→RB2; Najee→BN (worse rank); Flowers→FLEX1
start_sit: week 7; Mahomes→BN (bye); Love→QB1; diggs→BN (Out)
```

### No changes

```text
no_change: week {N}; lineup already optimal
```

Example:

```text
no_change: week 3; all starters healthy, no byes, best players already slotted
```

### Partial / locked constraints

```text
start_sit: week {N}; partial; left {Player} locked at {SLOT}; set {Other}→{SLOT}
```

## Reasoning structure

Write reasoning so a later review can audit the call without re-pulling tools:

1. **Week / phase**: e.g. Week 5, phase free_agency  
2. **Flags**: who is on bye, out, Q/D, locked  
3. **Research** (if any): one line per SearchWeb conclusion  
4. **Moves**: each SetPlayerSlot intent  
5. **No-change justification**: if applicable  
6. **Risks**: e.g. Questionable starter you kept active  

## Preferred final message (always emit)

```markdown
## Lineup decision (Week {week})
**Outcome:** changed | no_change
**Action:** <Action string for the decisions table>
**Changes:**
- Player Name (`sleeperId`): OLD_SLOT → NEW_SLOT — reason
- (or "None")
**Notable start/sit calls:**
- ...
**Why:**
- ...
**Open risks:**
- ...
```

## Logging pathway

1. **Host/season runner (current default)**  
   After the agent run, host code typically stores:
   - `Reasoning` = full agent response text  
   - `Action` = fixed label such as `"Lineup Setting"` or a parsed Action line  

   Make the **Action** and **Why** easy to extract: put `**Action:**` on its own line.

2. **Agent tool (when available)**  
   If a tool such as `LogDecision` is registered, call it once at the end with:
   - `type`: `start_sit` (unless overridden)
   - `action`: Action template string
   - `reasoning`: the full structured summary
   - `week`: current week
   - `agentId`: your id  

3. **Never skip logging content**  
   "No changes" is still a decision. Use the `no_change` Action template.

## Decision Type vocabulary (project conventions)

Common `Type` values elsewhere in the system include: `draft_pick`, `roster_add`, `roster_drop`, `bench_swap`, `start_sit`, plus runner labels (`Waiver Claim Attempt`, `Set Lineup for Sunday Games`, …).

For this skill prefer **`start_sit`** when you control Type; accept host-provided Type when the daily runner supplies one.
