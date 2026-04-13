# Step 3 – Create the Agents

## Decisions Made

- **Framework**: Microsoft.Agents.AI (already in csproj)
- **Architecture**: Hybrid — one shared `FantasyAgent` class instantiated 10 times, with per-agent workspace folders for static assets
- **LLM models**: 10 models selected from OpenRouter (see Agent Roster below)
- **Logo generation model**: `google/gemini-3.1-flash-image-preview`

## Architecture: Hybrid Agent Design

### One shared agent class, 10 configurations

Rather than 10 separate agent implementations, we build **one `FantasyAgent` class** that is instantiated 10 times from configuration. Each instance gets:

- A unique agent ID
- A workspace folder path
- A model name (to be filled in later)
- Shared tool access (MCP client, search, etc.)

### Per-agent workspace folders for static assets

Each agent gets a folder under `src/AgenticLeague/Workspaces/{agentId}/`:

```
Workspaces/
  agent-01/
    profile.json        ← Agent identity (team name, model, created date)
    strategy.md         ← Self-generated strategy document
    logo.png            ← Self-generated team logo
    memory/
      weekly-notes/     ← Per-week reflections and observations
      decisions.jsonl   ← Append-only decision log
  agent-02/
    ...
```

### Bootstrap flow (shared, not per-agent)

A shared `AgentWorkspaceInitializer` handles first-launch for any agent:

1. Check if `Workspaces/{agentId}/` exists → create if not
2. Check if `profile.json` exists → if not, agent generates team name and profile via LLM call
3. Check if `strategy.md` exists → if not, agent generates its own strategy via LLM call
4. Check if `logo.png` exists → if not, agent generates logo via `google/gemini-3.1-flash-image-preview`
5. Mark agent as initialized in `profile.json`

On subsequent runs, the initializer detects existing files and skips creation.

### Why not DB for everything?

Per-agent folders keep strategy and memory **human-readable and git-friendly** for the writing/analysis use case. The decision log uses append-only JSONL so concurrent writes are safe. If we need cross-agent queries later, we can add a DB-backed memory store behind an `IAgentMemoryStore` interface without changing the agent class.

## Service Architecture

Agents interact with the **LeagueAPI** — a consolidated service that owns players, scores, rosters, standings, draft, and decisions. See `LeagueAPI.md` for full details.

```
┌─────────────────┐          ┌─────────────────┐          ┌─────────────┐
│  Orchestrator   │          │    LeagueAPI     │          │  Front-end  │
│ (AgenticLeague) │──MCP────▶│  (MCP + REST)    │◀──REST──│  (React?)   │
│  10 agents      │          │  Players, Scores │          │             │
│                 │          │  Rosters, Draft  │          │             │
│                 │          │  Standings, Log  │          │             │
└─────────────────┘          └─────────────────┘          └─────────────┘
```

Agents use MCP tools to:
- Look up players and stats (from Sleeper + Yahoo data)
- Query their roster and available players
- Make draft picks, set lineups, claim players
- Log decisions with reasoning
- Check standings and matchups

## Folder & Class Structure

```
src/AgenticLeague/
  Program.cs                          ← Orchestrator entry point
  Configuration/
    AgentOptions.cs                   ← Strongly-typed config for all 10 agents
    AgentDefinition.cs                ← Per-agent config (id, model, persona hint)
  Agents/
    FantasyAgent.cs                   ← Core agent class (LLM loop + tool dispatch)
    AgentWorkspaceInitializer.cs      ← Bootstrap logic (check/create files)
    AgentFactory.cs                   ← Creates FantasyAgent from AgentDefinition
  Memory/
    IAgentMemoryStore.cs              ← Interface for memory read/write
    FileAgentMemoryStore.cs           ← File-backed implementation
  Tools/
    LeagueApiClient.cs                ← MCP client connecting to LeagueAPI
    SearchTool.cs                     ← Perplexity Sonar integration (stub initially)
  Models/
    AgentProfile.cs                   ← Team name, logo path, model, created date
    Decision.cs                       ← Decision log entry model
  Workspaces/                         ← Created at runtime, gitignored
    agent-01/
    agent-02/
    ...
```

## Agent Configuration (appsettings.json)

```json
{
  "Agents": {
    "OpenRouterApiKey": "env:OPENROUTER_API_KEY",
    "Definitions": [
      { "AgentId": "agent-01", "ModelName": "anthropic/claude-opus-4.6", "PersonaHint": "aggressive drafter" },
      { "AgentId": "agent-02", "ModelName": "anthropic/claude-sonnet-4.6", "PersonaHint": "analytics-focused" },
      { "AgentId": "agent-03", "ModelName": "openai/gpt-5.4", "PersonaHint": "TBD" },
      { "AgentId": "agent-04", "ModelName": "google/gemini-3.1-pro-preview", "PersonaHint": "TBD" },
      { "AgentId": "agent-05", "ModelName": "x-ai/grok-4.20", "PersonaHint": "TBD" },
      { "AgentId": "agent-06", "ModelName": "nvidia/nemotron-3-super-120b-a12b:free", "PersonaHint": "TBD" },
      { "AgentId": "agent-07", "ModelName": "moonshotai/kimi-k2.5", "PersonaHint": "TBD" },
      { "AgentId": "agent-08", "ModelName": "deepseek/deepseek-v3.2", "PersonaHint": "TBD" },
      { "AgentId": "agent-09", "ModelName": "z-ai/glm-5.1", "PersonaHint": "TBD" },
      { "AgentId": "agent-10", "ModelName": "qwen/qwen3.6-plus", "PersonaHint": "TBD" }
    ]
  }
}
```

The `PersonaHint` gives the LLM a nudge when generating its strategy, but the agent is free to interpret it however it wants.

## Agent Roster

| Agent | Model | Provider |
|-------|-------|----------|
| agent-01 | `anthropic/claude-opus-4.6` | Anthropic |
| agent-02 | `anthropic/claude-sonnet-4.6` | Anthropic |
| agent-03 | `openai/gpt-5.4` | OpenAI |
| agent-04 | `google/gemini-3.1-pro-preview` | Google |
| agent-05 | `x-ai/grok-4.20` | xAI |
| agent-06 | `nvidia/nemotron-3-super-120b-a12b:free` | NVIDIA |
| agent-07 | `moonshotai/kimi-k2.5` | Moonshot AI |
| agent-08 | `deepseek/deepseek-v3.2` | DeepSeek |
| agent-09 | `z-ai/glm-5.1` | Zhipu AI |
| agent-10 | `qwen/qwen3.6-plus` | Alibaba (Qwen) |

## Bootstrap Sequence Detail

```
Program starts
  → Load AgentOptions from config
  → For each AgentDefinition:
      1. AgentWorkspaceInitializer.EnsureWorkspaceAsync(agentId)
         - Create folder if missing
         - If no profile.json:
             → Call LLM: "You are a fantasy football team manager. Generate a team name and brief bio."
             → Save response to profile.json
         - If no strategy.md:
             → Call LLM: "Based on your persona ({personaHint}), define your draft and season strategy."
             → Save response to strategy.md
         - If no logo.png:
             → Call `google/gemini-3.1-flash-image-preview` with team name + strategy theme
             → Save to logo.png
      2. AgentFactory.CreateAsync(definition, workspace)
         → Load profile.json, strategy.md into agent context
         → Wire up MCP client to LeagueAPI
         → Wire up tools (Search)
         → Register agent profile with LeagueAPI (if first launch)
         → Return FantasyAgent instance
  → Orchestrator holds all 10 agents, ready for draft/season events
```

## Memory Design

### What gets stored
- **Weekly notes**: After each simulated week, agent reflects on outcomes (markdown files in workspace)
- **Decision log**: Every significant decision with reasoning — pushed to **LeagueAPI** (central, queryable) and also kept locally in workspace for agent memory context
- **Player evaluations**: Agent's opinions on players (can be part of weekly notes)

### How memory is used
- On each agent turn, the orchestrator loads relevant memory into context
- Initially: dump recent weekly notes + last N decisions (simple, no RAG)
- Later: can add summarization or vector search if context windows fill up

### Interface
```csharp
public interface IAgentMemoryStore
{
    Task<string> GetRecentMemoryAsync(string agentId, int weekNumber);
    Task AppendDecisionAsync(string agentId, Decision decision);
    Task SaveWeeklyNotesAsync(string agentId, int weekNumber, string notes);
}
```

File-backed implementation reads/writes from `Workspaces/{agentId}/memory/`.

## Decision Log Schema

Each entry in `decisions.jsonl`:
```json
{
  "timestamp": "2025-09-04T14:30:00Z",
  "agentId": "agent-01",
  "week": 1,
  "type": "draft_pick",
  "context": "Round 3, Pick 5. Available: [player list summary]",
  "reasoning": "Selected RB because my strategy prioritizes...",
  "action": "Drafted player X (sleeper_id: 1234)",
  "outcome": null
}
```

`outcome` is filled in later when results are known (e.g., player scored X points).

## Sub-Tasks

| # | Task | Depends On | Notes |
|---|------|-----------|-------|
| 3.1 | Create `AgentDefinition` and `AgentOptions` config models | — | ✅ Done |
| 3.2 | Create `AgentProfile` and `Decision` data models | — | ✅ Done |
| 3.3 | Build `AgentWorkspaceInitializer` | 3.1, 3.2 | Check/create folders, profile, strategy |
| 3.4 | Build `FantasyAgent` core class | 3.1 | LLM call loop via Microsoft.Agents.AI + OpenRouter |
| 3.5 | Build `AgentFactory` | 3.3, 3.4 | Wires up agent from config + workspace + LeagueAPI MCP client |
| 3.6 | Implement `IAgentMemoryStore` (file-backed) | 3.2 | Read/write memory files in workspace |
| 3.7 | Implement `LeagueApiClient` (MCP client) | 3.4, LeagueAPI | Connects to LeagueAPI MCP server for all league operations |
| 3.8 | Implement `SearchTool` stub (Perplexity Sonar) | 3.4 | Stub for now, wire up later |
| 3.9 | Wire up orchestrator in Program.cs | 3.5 | Load all agents, ready for events |
| 3.10 | Add agent isolation rules | 3.9 | Agents can only access own workspace + own roster via LeagueAPI |
| 3.11 | Add error handling and retries | 3.4 | Retry on LLM failures, log errors |

## Still Open / Deferred

- ~~Which 10 OpenRouter models~~ — **resolved**, see Agent Roster
- ~~Logo generation API~~ — **resolved**, use `google/gemini-3.1-flash-image-preview`
- ~~Score lookup tools~~ — **resolved**, agents query LeagueAPI which owns Yahoo scores (see `LeagueAPI.md`)
- **Perplexity Sonar API details** — stubbed for now
- **Orchestrator trigger model** — event-driven vs schedule, detailed in Step 4
