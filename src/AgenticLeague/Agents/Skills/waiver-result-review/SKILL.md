---
name: waiver-result-review
description: Interpret host-grouped raw waiver-claim JSON for one agent and take only the required follow-up actions. Use after waiver processing when reviewing claim outcomes. Do not use to choose, submit, replace, or prioritize waiver claims; use weekly-player-management for acquisition decisions.
metadata:
  author: agentic-league
  version: "1.1"
  domain: fantasy-football
---

# Waiver Result Review

Interpret the supplied waiver-result JSON as the authoritative record of the agent's waiver outcome. Determine whether the roster changed, take only necessary follow-up actions, and record a concise decision.

This skill is post-processing only. It does not decide which players to target or submit new claims.

## Input contract

The host calls the raw waiver-results endpoint once, groups its records by `agentId`, and supplies one object per agent:

```json
{
  "agentId": "player-01",
  "season": 2025,
  "week": 3,
  "phase": "free_agency",
  "waiversProcessedAtUtc": "2025-09-17T12:00:00+00:00",
  "claims": [
    {
      "waiverClaimId": "00000000-0000-0000-0000-000000000000",
      "agentId": "player-01",
      "season": 2025,
      "week": 3,
      "claimOrder": 1,
      "addSleeperPlayerId": "1234",
      "dropSleeperPlayerId": "5678",
      "priorityAtSubmission": 4,
      "status": "Successful",
      "failureReason": null,
      "submittedAtUtc": "2025-09-17T10:00:00+00:00",
      "processedAtUtc": "2025-09-17T12:00:00+00:00"
    }
  ]
}
```

The `claims` array contains every raw result for this agent and waiver period. Player identifiers are opaque values: do not assume they are player names or positions. Read [waiver result schema](references/waiver-result-schema.md) before interpreting an unfamiliar payload or field.

## Scope and hard constraints

- Treat the supplied JSON as the authoritative outcome for the indicated season and week.
- Treat `claims` as the complete result set for this agent. Do not expect claims for other agents or request another waiver-results payload.
- Do not submit, replace, or prioritize claims in this skill.
- Do not add or drop players directly. Waiver processing already applies successful claims.
- Do not claim a player was added unless the claim status is `Successful`, ignoring case.
- Do not make lineup changes until a roster read confirms the successful transaction.
- Use the exact `agentId` from the JSON when calling tools.

## Required tools

| Tool | Purpose |
|------|---------|
| `GetMyRoster` | Confirm all applied successful adds/drops with one roster read and identify lineup impact. |
| `ReadAgentBootstrap` / `WriteAgentBootstrap` | Read strategy and preserve a short durable result note. |
| `GetLeagueState` | Use only if the JSON is missing phase, season, or week, or current state affects a follow-up. |
| `roster-management` skill | Load only after a confirmed successful acquisition could change the starting lineup. |
| `weekly-player-management` skill | Load only when the host explicitly asks for a new acquisition decision after review. |

## Workflow

### 1. Validate and classify the payload

1. Confirm `agentId`, `season`, and `week` are present.
2. Confirm every claim has the same `agentId`, `season`, and `week` as the outer object. A mismatch is `blocked`; report it and do not infer an outcome.
3. Note `phase` and `waiversProcessedAtUtc`.
4. If `claims` is empty, classify the outcome as `no_claims`.
5. Otherwise, examine claims in `claimOrder` order and classify each status case-insensitively:

| Status | Meaning | Required behavior |
|--------|---------|-------------------|
| `Successful` | This add/drop was applied. | Confirm with `GetMyRoster`; assess lineup impact. |
| `Pending` | Processing is incomplete or the claim is still pending. | Do not assume a roster change; take no transaction action. |
| `Failed` | This claim did not apply. | Record `failureReason`; take no roster action. |
| `Superseded` | Another successful claim in this waiver period made this claim inapplicable. | Do not describe it as a second successful add. |

Unknown or missing statuses are `blocked`: report the payload field exactly, do not infer an outcome, and stop before roster or lineup follow-up.

### 2. Handle no claims or unresolved processing

If `claims` is empty:

- Do not call roster or transaction tools.
- State that no claim was submitted for this waiver period.
- Record a `no_claims` decision summary.

If any claim is `Pending` or its `processedAtUtc` is missing:

- Do not call `GetMyRoster` solely to infer the outcome of that unresolved claim.
- Do not add, drop, or set a lineup.
- State which claims remain unresolved and wait for completed results.
- Record `pending` when all claims are unresolved; otherwise record `mixed`.

### 3. Confirm successful transactions

If one or more claims are `Successful`:

1. Call `GetMyRoster(agentId)` exactly once for all successful claims.
2. Confirm every `addSleeperPlayerId` is rostered and every non-null `dropSleeperPlayerId` is absent.
3. If the roster does not match a recorded success, state the discrepancy; do not repair it with direct add/drop tools.
4. A newly acquired player starts on `BN` unless a later lineup review moves them.

### 4. Decide whether a lineup review is required

Load `roster-management` only when all are true:

1. At least one successful claim is confirmed in the roster.
2. The added player could plausibly improve a currently fillable starter slot, replace a bye/Out player, or provide needed injury coverage.
3. No claim remains unresolved.
4. The task allows lineup management in the current phase.

Otherwise, leave lineup slots unchanged. Never invoke `roster-management` for failed, superseded, pending, or unconfirmed claims.

### 5. Preserve memory

Read the bootstrap before writing. After completed outcomes, append one concise dated note under the existing decision log:

```text
- YYYY-MM-DD (Week N Waivers): Claim #{order} {status}; added ID {addSleeperPlayerId} / dropped ID {dropSleeperPlayerId or "none"}. Reason: {short rationale}.
```

For failures or no claims, write only when the outcome creates a lasting strategy change, such as a confirmed long-term injury need. Do not overwrite existing bootstrap content.

### 6. Record the decision

End every run with this exact structure:

```markdown
## Waiver result review (Week {week})
**Loaded skill:** waiver-result-review
**Outcome:** successful | pending | no_claims | failed | mixed | blocked
**Action:** <one-line factual outcome for the decision table>
**Submission priority:** {priorityAtSubmission}, "mixed", or "unavailable"
**Claim outcomes:**
- #{claimOrder}: Add ID {addSleeperPlayerId} / Drop ID {dropSleeperPlayerId or "none"} — {status}; {failure reason if present}
- (or "No claims submitted")
**Roster confirmation:**
- <confirmed add/drop, "Not required", or discrepancy>
**Lineup follow-up:**
- <roster-management run, "Not needed", or reason deferred>
**Why:**
- <evidence from the JSON and roster confirmation>
**Open risks:**
- <pending claim, mismatch, injury/bye consideration, or "None">
```

Use `waiver_result` as the decision Type when the host supports it.

## Common failure modes

| Mistake | Correct behavior |
|---------|-------------------|
| Treating `pending` as an acquired player | Wait for processed results. |
| Treating all claims as successful | Only `Successful`, ignoring case, means the transaction applied. |
| Calling acquisition tools after a failed claim | Do not reopen acquisition decisions unless explicitly asked; then load `weekly-player-management`. |
| Starting a successful add immediately | Confirm with `GetMyRoster`, then use `roster-management` only if lineup review is warranted. |
| Calling direct add/drop tools to repair an API mismatch | Report the mismatch; preserve the authoritative waiver result. |
| Treating a superseded fallback as another add | It is not a successful transaction. |
| Assuming the raw payload contains player names or current waiver priority | Use the supplied IDs and submission priority only. |
