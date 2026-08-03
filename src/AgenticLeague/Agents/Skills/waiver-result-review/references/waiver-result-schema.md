# Raw Waiver Result JSON Schema

The LeagueAPI raw waiver-results endpoint returns a flat list of claim results. The host groups that list by `agentId` and supplies one grouped object to `waiver-result-review`.

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

## Top-level fields

| Field | Meaning |
|------|---------|
| `agentId` | Agent whose claims the host grouped into this object. |
| `season`, `week` | Waiver period identity. |
| `phase` | Host-provided league phase for follow-up work. |
| `claims` | All raw claim results for this agent and waiver period, ordered by `claimOrder`. |
| `waiversProcessedAtUtc` | Host-provided processing time. The claim-level `processedAtUtc` remains authoritative for that claim. |

## Claim fields

| Field | Meaning |
|------|---------|
| `waiverClaimId` | Unique claim-result identifier. |
| `agentId`, `season`, `week` | Must match the grouped outer object. A mismatch makes the payload invalid for review. |
| `claimOrder` | Preference order supplied by the agent; 1 is highest priority. |
| `addSleeperPlayerId` | Opaque identifier for the requested addition. The payload does not include player names. |
| `dropSleeperPlayerId` | Opaque identifier for the requested removal; `null` means no drop was required. |
| `priorityAtSubmission` | Waiver priority when the claim was submitted. |
| `status` | Outcome: `Successful`, `Pending`, `Failed`, or `Superseded`. Compare case-insensitively. |
| `failureReason` | Human-readable reason a failed claim could not apply; may be `null`. |
| `submittedAtUtc`, `processedAtUtc` | Audit timestamps; processing time may be `null`. |

## Interpretation examples

### Successful primary claim

`status: "Successful"` means the add/drop has already been applied. Verify the player IDs with `GetMyRoster`; do not call an add/drop tool.

### Superseded fallback

`status: "Superseded"` means an earlier claim succeeded, so this fallback did not apply. Do not describe its add-player ID as rostered.

### Pending processing

`status: "Pending"` or `processedAtUtc: null` means that claim is not final. Do not infer a roster change from its requested add/drop.
