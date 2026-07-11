---
name: post-draft-review
description: Convert draft-time working notes into a concise in-season game plan after the draft is complete. Use only after a completed draft to document the roster, revise strategy from actual draft results, and set the initial lineup through roster-management.
metadata:
  author: agentic-league
  version: "1.0"
  domain: fantasy-football
---

# Post-Draft Review

Transition the agent from draft mode to in-season management. Preserve its durable identity, league settings, strategy, and meaningful history while replacing verbose pick-by-pick draft notes with a concise current roster reference.

## Scope and hard constraints

- Use only after the draft is complete.
- Do not add, drop, claim, trade, or otherwise acquire players.
- Do not invent roster data, injury information, player roles, or draft results.
- Do not delete the team identity, logo path, league settings, decision history, or meaningful strategy updates.
- Do not use `AutoSetLineup`; delegate lineup assignment to `roster-management`.

## Required tools

| Tool | Purpose |
|------|---------|
| `GetLeagueState` | Confirm the current league context. |
| `ReadAgentBootstrap` | Load the existing game plan and draft-time working notes. |
| `GetMyRoster` | Load the authoritative final roster and current slot assignments. |
| `WriteAgentBootstrap` | Persist the concise in-season game plan. |
| `SearchWeb` | Optional: verify material injury or role uncertainty for roster notes. |

## Workflow

1. Call `GetLeagueState`, `ReadAgentBootstrap(agentId)`, and `GetMyRoster(agentId)`.
2. Confirm the roster is the source of truth for player names, IDs, positions, teams, and slot assignments.
3. Ask the `roster-management` skill to set and verify the complete initial lineup from the current roster. Do not call `AutoSetLineup`.
4. Re-read `GetMyRoster(agentId)` after lineup management so the game plan reflects the verified final slots.
5. Build a concise `## Current Roster` table using the verified roster:

   ```markdown
   ## Current Roster

   | Slot | Player | Position | Team | Notes |
   |------|--------|----------|------|-------|
   | QB1 | <player> | QB | <team> | <brief forward-looking note> |
   | RB1 | <player> | RB | <team> | <brief forward-looking note> |
   | ... | ... | ... | ... | ... |
   | BN | <player> | <position> | <team> | <brief forward-looking note> |
   ```

6. Include every rostered player once. Use the current `slotType`; use `BN` for bench players. Keep each note brief and factual, based only on roster data or targeted research: bye week, injury status, role uncertainty, depth-chart order, upside, or handcuff context.
7. Review the existing strategy against the actual roster:
   - Update `## Core Strategy` or `## In-Season Operating Plan` only when draft results materially changed the original plan.
   - Record actionable strengths, weaknesses, injury risks, thin positions, and early waiver priorities in `## Roster and Team Context`.
   - Do not turn the strategy into a pick-by-pick recap.
8. Remove verbose draft-round/pick reasoning that is no longer useful. Preserve material decisions in `## Decision Log`.
9. Add one concise dated note under `## Decision Log`, for example:

   ```markdown
   - YYYY-MM-DD: Post-draft review complete. Draft notes condensed into the current roster table; initial lineup set and strategy confirmed.
   ```

10. Add a `## Strategy Updates` note only if the actual roster changed the long-term strategy.
11. Write the complete updated `bootstrap.md` with `WriteAgentBootstrap(agentId, content)`. Preserve all durable sections; do not overwrite the document with only the roster table.

## Required document shape

The completed game plan must retain or contain:

```markdown
# <Team Name> Game Plan

## Identity
## League Settings
## Core Strategy
## In-Season Operating Plan
## Current Roster
## Roster and Team Context
## Decision Log
## Strategy Updates
```

## Required decision summary

End with this exact structure:

```markdown
## Post-draft review
**Loaded skill:** post-draft-review
**Outcome:** completed | partial | blocked
**Roster summary:**
- <key strengths and weaknesses>
**Lineup:**
- <initial lineup set and verified, or why it could not be completed>
**Strategy:**
- confirmed | updated — <brief reason>
**Game plan update:**
- <what was condensed or updated>
**Open risks:**
- <injuries, thin positions, pending research, or "None">
```

## Common failure modes

| Mistake | Correct behavior |
|---------|------------------|
| Treating draft-time notes as the final roster | Use `GetMyRoster` as the roster source of truth. |
| Leaving the entire roster on the bench | Delegate a complete initial setup to `roster-management`. |
| Writing a roster table without all rostered players | Include every player exactly once. |
| Replacing the entire game plan with a table | Preserve identity, league settings, strategy, and meaningful history. |
| Inventing detailed player notes | Use only roster data or targeted research. |
| Making post-draft acquisitions | Leave adds/drops to `weekly-player-management`. |
