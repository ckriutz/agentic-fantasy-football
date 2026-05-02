# Project Instructions

## General Principles

- Keep things simple. Do not over-engineer solutions.
- Ask before doing things that might be complex or introduce significant changes.
- Prefer straightforward, readable code over clever abstractions.
- Only make changes that are directly requested or clearly necessary.
- Do not add features, refactor code, or make "improvements" beyond what is asked.

## Tech Stack

- .NET (C#) for backend services
- ASP.NET Core minimal APIs
- Postgres for persistence
- Sleeper API for player data sync

## Project Structure

- `src/LeagueAPI/` — Player data service with Sleeper API integration
- `src/AgenticLeague/` — Agent configuration and models

## Testing
Right now, testing is mostly manual. We will run the program and observe the agents' behavior and the draft process. In the future, we may want to add automated tests for some of the components, but for now, we will focus on manual testing and observation. Do not create automated tests unless explicitly asked to do so.

## Note on creating functions/methods
Casey prefers to have the function/method to be on one line:

Example of what not to do:
```csharp
static async Task RunDraftPickWithRetriesAsync(
    AIAgent agent,
    string draftPrompt,
    int round,
    int pick,
    int maxAttempts,
    IReadOnlyList<TimeSpan> retryBackoffs,
    ILogger logger)
{
}
```

Example of what to do:
```csharp
static async Task RunDraftPickWithRetriesAsync(AIAgent agent, string draftPrompt, int round, int pick, int maxAttempts, IReadOnlyList<TimeSpan> retryBackoffs, ILogger logger)
{
}
```

## Casey's Notes
- Often misspells Leauge as League. Be mindful of this when working on the codebase.
