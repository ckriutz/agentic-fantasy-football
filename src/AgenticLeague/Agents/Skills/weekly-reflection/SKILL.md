---
name: weekly-reflection
description: Review a completed fantasy-football week, evaluate the team's matchup and player usage, and update the durable game plan for the upcoming week. Use after weekly results are finalized and before waiver or free-agency decisions. Do not make roster, lineup, or transaction changes.
metadata:
  author: agentic-league
  version: "1.0"
  domain: fantasy-football
---

# Weekly Reflection

Reflect on the completed NFL week and turn the result into a focused plan for the upcoming week. This skill is analysis and durable-memory work only. Do not add, drop, claim, trade, or move players between lineup slots.

## Required tools

| Tool | Purpose |
|------|---------|
| `ReadAgentBootstrap` | Load the team's identity, strategy, roster context, and prior reflections. |
| `GetWeeklyMatchup` | Review the completed week's opponent, points, and result. |
| `GetMyRoster` | Compare each player's completed-week production with their current slot. |
| `WriteAgentBootstrap` | Persist the reflection and any lasting strategy update. |

## Workflow

1. Use the exact `agentId` supplied by the task.
2. Call `ReadAgentBootstrap(agentId)` before making any assessment.
3. Call `GetWeeklyMatchup(agentId, completedWeek)` and `GetMyRoster(agentId)`.
4. Classify the matchup as a win, loss, tie, incomplete, or unavailable. Treat the matchup result and points returned by `GetWeeklyMatchup` as authoritative; do not infer a result from standings or memory.
5. Evaluate the roster using the completed week's `WeeklyPoints[completedWeek]` values:
   - Compare starters with bench players at the same position.
   - Identify meaningful start/sit wins and misses.
   - Note injuries, byes, locked players, unexpected roles, and underperformance only when supported by tool data.
   - Do not overreact to one outlier performance or treat fantasy points alone as proof that a process was wrong.
6. Write one concise dated entry under `## Decision Log` containing:
   - completed season and week;
   - matchup result and score;
   - the most important evidence from the roster review;
   - the next-week priorities.
7. Update `## Strategy Updates` only when the reflection supports a lasting change to the team's approach. Preserve the existing identity, strategy, roster context, and prior decision history.
8. Call `WriteAgentBootstrap(agentId, completeContent)` once with the preserved document and the new reflection.
9. Do not make transactions or lineup changes in this run. Leave those actions to `weekly-player-management` and `roster-management`.

## Required decision summary

End every run with this exact structure:

```markdown
## Weekly reflection (Season {season}, Week {completedWeek})
**Loaded skill:** weekly-reflection
**Result:** win | loss | tie | incomplete | unavailable
**Score:** {my points} - {opponent points}
**What worked:**
- <evidence-based strengths>
**What did not work:**
- <evidence-based weaknesses or "None">
**Key player lessons:**
- <starter/bench, injury, role, or process observations>
**Plan for Week {upcomingWeek}:**
- <specific priorities for roster, lineup, research, or waivers>
**Bootstrap update:**
- <reflection written, strategy updated, or why no durable update was needed>
**Open risks:**
- <current uncertainties or "None">
```

If the matchup is incomplete or unavailable, say so explicitly and do not invent a score or result. A reflection is still useful when the team did not win, but the conclusion must distinguish process quality from outcome luck.

