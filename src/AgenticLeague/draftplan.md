# Draft Resilience Plan

## Problem
The draft loop in `Program.cs` runs 150 sequential agent picks (15 rounds × 10 agents). When an LLM provider times out (default 100s), the entire draft crashes with no way to resume. Two improvements needed:
1. **Better execution model** than a bare `for`/`foreach` loop
2. **Better error handling** so one failed pick doesn't kill the whole draft

## Approach
Since agents query the shared DB to see taken players, each pick is already state-aware. This means we can safely retry, skip, and resume without losing consistency.

### 1. Increase network timeout (`FantasyAgent.cs`)
The default `ClientPipelineOptions.NetworkTimeout` is 100s. LLM calls through OpenRouter with tool use can legitimately take longer. Bump it to 5 minutes in the `OpenAIClientOptions`.

### 2. Add retry-with-backoff per pick (`Program.cs`)
Wrap each agent's `RunAsync` call in a retry loop (3 attempts, exponential backoff). This handles transient timeouts without skipping the pick entirely.

### 3. Catch and skip on permanent failure (`Program.cs`)
If all retries fail, log a warning and move to the next pick instead of crashing. The agent can potentially make up for it in later rounds.

### 4. Persist draft progress for resumability (`Program.cs`)
Before the draft starts, save the randomized agent order and current pick to a simple JSON state file (`draft-state.json`). After each successful pick, update the file. On startup, if the file exists, resume from where it left off instead of re-randomizing and starting from pick 1.

### 5. Extract a `DraftRunner` helper (new file)
Move the draft loop, retry logic, and state persistence into a dedicated `DraftRunner` class to keep `Program.cs` clean.

## Files to Change

| File | Change |
|---|---|
| `src/AgenticLeague/Agents/FantasyAgent.cs` | Increase `NetworkTimeout` to 5 min in `OpenAIClientOptions` |
| `src/AgenticLeague/DraftRunner.cs` | **New file** — draft loop with retry, error handling, and state persistence |
| `src/AgenticLeague/Models/DraftState.cs` | **New file** — simple model for persisted draft state |
| `src/AgenticLeague/Program.cs` | Replace inline draft loop with `DraftRunner` call |

## Design Notes
- Draft state file is a simple JSON file (no DB dependency) — keeps it self-contained
- Retry uses exponential backoff: 10s → 30s → 90s
- Max retries: 3 per pick (configurable)
- Both `TaskCanceledException` (timeout) and `ArgumentOutOfRangeException` (ChatFinishReason) are caught
- On permanent failure after retries, the pick is skipped and logged — draft continues
