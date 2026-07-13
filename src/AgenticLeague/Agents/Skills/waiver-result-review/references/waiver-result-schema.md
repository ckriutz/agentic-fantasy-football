# Waiver Result JSON Schema

The LeagueAPI endpoint returns one object for one agent and waiver period.

```json
{
  "agentId": "player-01",
  "season": 2025,
  "week": 3,
  "phase": "free_agency",
  "myPriority": 4,
  "totalAgents": 10,
  "hasPendingClaims": false,
  "myClaims": [
    {
      "waiverClaimId": "00000000-0000-0000-0000-000000000000",
      "claimOrder": 1,
      "addPlayer": {
        "sleeperPlayerId": "1234",
        "fullName": "Example Add",
        "team": "EX",
        "position": "WR"
      },
      "dropPlayer": {
        "sleeperPlayerId": "5678",
        "fullName": "Example Drop",
        "team": "EX",
        "position": "WR"
      },
      "priorityAtSubmission": 4,
      "status": "successful",
      "failureReason": null,
      "submittedAtUtc": "2025-09-17T10:00:00+00:00",
      "processedAtUtc": "2025-09-17T12:00:00+00:00",
      "wasSuccessful": true,
      "wasSuperseded": false
    }
  ],
  "waiversProcessedAtUtc": "2025-09-17T12:00:00+00:00"
}
```

## Top-level fields

| Field | Meaning |
|------|---------|
| `agentId` | Agent whose claims are reported. |
| `season`, `week` | Waiver period identity. |
| `phase` | League phase at the time the summary was produced. |
| `myPriority` | Current waiver priority for this agent; can be `null`. |
| `totalAgents` | Number of agents in the priority order. |
| `hasPendingClaims` | Whether any claim still has `pending` status. |
| `myClaims` | Claims submitted by this agent for the period, ordered by `claimOrder` after sorting. |
| `waiversProcessedAtUtc` | Completion time for processing; `null` means processing has not completed. |

## Claim fields

| Field | Meaning |
|------|---------|
| `claimOrder` | Preference order supplied by the agent; 1 is highest priority. |
| `addPlayer` | Player requested for addition. |
| `dropPlayer` | Player requested for removal; `null` means no drop was required. |
| `priorityAtSubmission` | Waiver priority when the claim was submitted. |
| `status` | Outcome: `successful`, `pending`, `failed`, or `superseded`. |
| `failureReason` | Human-readable reason a failed claim could not apply; may be `null`. |
| `submittedAtUtc`, `processedAtUtc` | Audit timestamps; processing time may be `null`. |
| `wasSuccessful`, `wasSuperseded` | Convenience flags that must agree with `status`; prefer `status` if they conflict. |

## Interpretation examples

### Successful primary claim

`status: "successful"` and `wasSuccessful: true` means the add/drop has already been applied. Verify with `GetMyRoster`; do not call an add/drop tool.

### Superseded fallback

`status: "superseded"` means an earlier claim succeeded, so this fallback did not apply. Do not describe its add player as rostered.

### Pending processing

`hasPendingClaims: true` or `waiversProcessedAtUtc: null` means results are not final. Do not infer a roster change from the requested add/drop.
