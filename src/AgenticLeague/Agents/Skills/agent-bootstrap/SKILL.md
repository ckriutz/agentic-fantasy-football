---
name: agent-bootstrap
description: Initialize or validate an agent's durable fantasy-football identity, bootstrap file, team name, strategy, and logo. Use only when explicitly asked to bootstrap an agent or verify bootstrap readiness.
metadata:
  author: agentic-league
  version: "1.0"
  domain: fantasy-football
---

# Agent Bootstrap

Create or validate the agent's durable identity and game plan. The bootstrap document is the agent's long-term working memory: use it throughout the season to review strategy, record material decisions, and update team context. This skill is lifecycle work only. Do not draft, set a lineup, make transactions, or perform routine roster management.

## Required tools

| Tool | Purpose |
|------|---------|
| `ReadAgentBootstrap` | Read the current `bootstrap.md` for the agent. |
| `WriteAgentBootstrap` | Create or update the agent's `bootstrap.md`. |
| `GenerateImage` | Generate the team logo and return its filename. |
| `SetMyTeamName` | Persist the selected team name in the agent profile. |
| `SetMyBootstrapStatus` | Mark the agent bootstrapped after every required step succeeds. |

## Workflow

The task supplies the `agentId`. Use that exact value for `ReadAgentBootstrap`, `WriteAgentBootstrap`, `SetMyTeamName`, and `SetMyBootstrapStatus`.

1. Call `ReadAgentBootstrap(agentId)` before doing anything else.
2. Before creating or repairing a bootstrap document, call `read_skill_resource` for `league-rules.md`. This reference is the source of truth for league settings. Copy its relevant facts accurately; do not invent or contradict league rules.
3. If `bootstrap.md` exists and is complete, verify that it includes all required content below. Extract its team name, call `SetMyTeamName(agentId, teamName)`, then call `SetMyBootstrapStatus(agentId, true)`. Respond with exactly:

   ```text
   ✅ <team name> is bootstrapped and ready to go!
   ```

4. If the file does not exist, choose a creative team name. It may be fantasy-football, sports, or otherwise inspirational themed, but it must not contain the word `Gridiron` in any capitalization.
5. Create a substantive winning strategy covering the core philosophy, draft strategy, and in-season operating plan.
6. Call `GenerateImage` with a concise logo description based on the team name and winning strategy. The logo must be simple and suitable for a fantasy-football website. Keep the filename returned by the tool.
7. Call `SetMyTeamName(agentId, teamName)`.
8. Build a complete bootstrap document using the template below. Include relevant facts from `league-rules.md`, but do not invent player names, roster information, or past decisions. Include the actual logo filename returned by `GenerateImage`; never write `Pending` as a logo path.
9. Call `WriteAgentBootstrap(agentId, completeContent)` once with the completed document.
10. Call `SetMyBootstrapStatus(agentId, true)` only after the team name was saved and the completed bootstrap file, including the generated logo filename, was successfully written. This is the final commit marker for bootstrap completion.
11. If any required tool call fails, do not call `SetMyBootstrapStatus`. Respond with the failure and what remains incomplete.
12. Respond with exactly:

    ```text
    ✅ <team name> is bootstrapped and ready to go!
    ```

## Recovering an incomplete bootstrap

If `bootstrap.md` exists but does not meet the completeness requirements:

1. Retain valid existing identity, strategy, league, roster-context, decision-log, and strategy-update content.
2. Choose a new team name only if the existing name is missing or invalid.
3. Generate a new logo only if the existing logo filename is missing or invalid.
4. Save the team name with `SetMyTeamName`, write the repaired complete document with `WriteAgentBootstrap`, then set status with `SetMyBootstrapStatus(agentId, true)`.
5. Do not set bootstrap status if any repair step fails.

## League rules reference

Use [league rules](references/league-rules.md) when creating or repairing the `## League Settings` section.

## Bootstrap document template

```markdown
# <Team Name> Game Plan

## Identity
- **Team Name**: <Team Name>
- **Agent ID**: <agentId>
- **Logo Path**: <generated filename>

## League Settings
- <Known league rules and scoring settings only>

## Core Strategy
### Core Philosophy
<How this team will win the league.>

### Draft Strategy
<Position priorities, player archetypes, and risk approach.>

## In-Season Operating Plan
<How to manage lineups, waivers, trades, injuries, and bye weeks.>

## Roster and Team Context
- No roster has been acquired yet.

## Decision Log
- No decisions recorded yet.

## Strategy Updates
- Initial game plan created during bootstrap.
```

## Completeness requirements

Treat a bootstrap file as complete only when it has:

- A non-empty team name that does not contain `Gridiron`
- The correct agent ID
- A non-empty logo filename, not `Pending`
- A substantive strategy with core, draft, and in-season operating-plan sections
- Roster/team context, a decision log, and a strategy-updates section ready for seasonal use

If an existing bootstrap file is incomplete, repair it to meet these requirements. Regenerate the logo only when the logo is missing. Set the profile team name and bootstrap status only after the document is complete.
