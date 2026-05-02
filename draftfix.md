# Draft resume/restart fix

The current draft runner can replay already-completed picks after a failure because it saves `Round` and `Pick`, but it does not resume from the correct agent within the round. It also keeps adding roster assignments across runs, which makes restarted drafts look larger than they should be.

## Recommended fix steps

1. **Store the next drafter position explicitly.**  
   Add enough state to resume mid-round without replaying earlier agents. The simplest option is a single `OverallPickNumber` or `NextDraftOrderIndex`; another good option is `Round` + `PickInRound`.

2. **Resume from the remaining agents only.**  
   In `RunDraftAsync()`, when loading saved state, compute which agents in the current round have already completed and start the loop from that point instead of iterating the whole round from the top.

3. **Only advance state after a successful roster assignment.**  
   Right now `Pick` advances even if `DraftPlayerAsync()` ultimately fails or skips. Change the flow so the draft state moves forward only when the player was actually added, or persist a clear failed-pick record if advancing on failure is intentional.

4. **Have `DraftPlayerAsync()` return a result instead of only logging.**  
   Return something like `Success`, `Failed`, or `Skipped` so the caller can decide whether to increment progress, retry later, or halt the draft.

5. **Scope roster assignments to a draft run.**  
   Add a `DraftRunId` to the draft state and to roster assignments, or clear draft-created assignments before a fresh restart. Without that, replayed or resumed picks accumulate and make it look like one draft produced too many players.

6. **Separate resume from restart-fresh behavior.**  
   These should be distinct flows:
   - **Resume:** continue from saved state and keep prior picks.
   - **Restart fresh:** delete draft-created roster assignments for that run, create a new `DraftRunId`, and reset state to pick 1.

7. **Persist draft order and progress together.**  
   Keep `DraftOrder`, `DraftRunId`, and the exact current draft position in the same saved state so every restart uses the same order and same next picker.

8. **Add a startup consistency check.**  
   On launch, compare saved draft state with roster rows for the current draft run. If state says pick 8 but only 6 successful draft assignments exist, stop and log a mismatch instead of silently replaying or advancing incorrectly.

9. **Log each completed pick with structured metadata.**  
   For every successful pick, log `DraftRunId`, `Round`, `Pick`, `AgentId`, and `SleeperPlayerId` so resume issues are easy to trace.

10. **Manually test restart scenarios.**  
    Verify:
    - failure before any pick
    - failure mid-round
    - failure between rounds

    In each case, confirm no already-completed drafter picks again and the total drafted player count matches the expected total.

## Minimum fix to start with

If you want the smallest useful fix first, do steps 1, 2, 3, and 5.
