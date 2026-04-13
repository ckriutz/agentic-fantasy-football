# LeagueAPI – Consolidated League Service

## Overview

The LeagueAPI consolidates what is currently the **PlayerDatabase** project with new responsibilities for **Yahoo scoring data**, **league state management**, and **commissioner rule enforcement**. It becomes the single API/MCP server that agents, the orchestrator, and the front-end all interact with.

### Why consolidate?

- PlayerDatabase already has a working MCP server, REST API, and Sleeper sync
- Yahoo scores, rosters, standings, and draft state all relate to the same player entities
- One database, one API surface, one MCP server = simpler deployment and fewer moving parts
- Agents only need to connect to one service instead of juggling multiple

### Database: PostgreSQL

PostgreSQL replaces SQL Server as the database engine. SQL Server's Docker image is x64-only, making it unreliable on ARM targets (MacBook M2, Raspberry Pi). PostgreSQL runs natively on ARM, has excellent .NET support via Npgsql / EF Core, and is free.

```
Docker image: postgres:17
.NET driver: Npgsql + Npgsql.EntityFrameworkCore.PostgreSQL
```

### What changes from PlayerDatabase

The existing `PlayerDatabase` project gets **renamed to `LeagueAPI`** and expanded. All existing Sleeper sync, player catalog, and MCP tooling remains intact. New capabilities are added alongside.

## Architecture

```
┌─────────────────────────────────────────────────────┐
│                      LeagueAPI                       │
│                                                      │
│  ┌──────────────┐  ┌──────────────┐  ┌────────────┐ │
│  │ Player       │  │ Yahoo Scores │  │ League     │ │
│  │ Catalog      │  │ Service      │  │ State      │ │
│  │ (Sleeper)    │  │              │  │ Service    │ │
│  │ [exists]     │  │ [new]        │  │ [new]      │ │
│  └──────┬───────┘  └──────┬───────┘  └─────┬──────┘ │
│         │                 │                │         │
│         ▼                 ▼                ▼         │
│  ┌──────────────────────────────────────────────┐   │
│  │             PostgreSQL Database               │   │
│  │  Players | Stats | Rosters | Standings | ...  │   │
│  └──────────────────────────────────────────────┘   │
│         │                 │                │         │
│         ▼                 ▼                ▼         │
│  ┌──────────────────────────────────────────────┐   │
│  │          REST API  +  MCP Server              │   │
│  └──────────────────────────────────────────────┘   │
└──────────────────────┬──────────────────────────────┘
                       │
          ┌────────────┼────────────┐
          ▼            ▼            ▼
    Agents (MCP)   Orchestrator   Front-end (REST)
```

## What the LeagueAPI Owns

### Domain 1: Player Catalog (existing)
Already built. Sleeper sync, player records, search/query.

**Existing MCP tools:**
- `GetPlayerBySleeperId`
- `GetPlayerByYahooId`
- `SearchPlayers`
- `GetLatestSleeperSyncStatus`

**Existing REST endpoints:**
- `GET /api/players/{sleeperPlayerId}`
- `GET /api/players/by-yahoo/{yahooId}`
- `GET /api/players?name=&team=&position=&limit=`
- `GET /api/sync/sleeper/latest`
- `POST /api/sync/sleeper`

No changes needed here. These stay as-is.

### Domain 2: Yahoo Scores (new)
Pulls weekly player stats from Yahoo Fantasy API and calculates fantasy points using our scoring template.

**New database tables:**
- `WeeklyPlayerStats` – raw stat lines per player per week (passing yards, TDs, etc.)
- `WeeklyPlayerPoints` – calculated fantasy points per player per week
- `ScoringTemplate` – stat modifiers for point calculation (e.g., passing TD = 4 pts)

**New services:**
- `YahooApiClient` – OAuth 2.0 authenticated client (port from test_yahoo_api.py to C#)
- `YahooScoreSyncService` – pulls weekly stats from `game/{game_key}/players/.../stats;type=week;week={n}`
- `ScoringService` – applies scoring template to raw stats → fantasy points

**New MCP tools:**
- `GetPlayerWeeklyStats(yahooId, week)` – raw stat line for a player in a given week
- `GetPlayerWeeklyPoints(yahooId, week)` – fantasy points for a player in a given week
- `GetTopScorersByWeek(week, position?, limit)` – leaderboard for a week
- `GetPlayerSeasonPoints(yahooId)` – total season points

**New REST endpoints:**
- `GET /api/stats/{yahooId}/week/{week}` – raw stats
- `GET /api/points/{yahooId}/week/{week}` – fantasy points
- `GET /api/points/leaders?week=&position=&limit=` – weekly leaderboard
- `GET /api/points/{yahooId}/season` – season totals
- `GET /api/scoring-template` – current scoring rules

**Configuration:**
- Yahoo OAuth credentials (client ID, client secret) via environment variables
- Game key (e.g., `449` for 2024, `461` for 2025) in appsettings
- Scoring template seeded on startup or configurable

### Domain 3: League State (new)
The core of the league — rosters, standings, schedule, draft, decisions. This is the "commissioner."

#### 3a. Rosters

**Database tables:**
- `Rosters` – which player belongs to which agent, with roster slot (QB, RB1, RB2, WR1, WR2, TE, FLEX, K, DEF, BN1-BN6)

**MCP tools (agent-facing):**
- `GetMyRoster(agentId)` – agent's current roster
- `SetLineup(agentId, week, starterSlots)` – set starters for a week
- `DropPlayer(agentId, sleeperPlayerId)` – drop a player
- `ClaimPlayer(agentId, sleeperPlayerId, dropPlayerId?)` – waiver/free agent pickup

**REST endpoints (front-end facing):**
- `GET /api/rosters/{agentId}` – agent's roster
- `GET /api/rosters` – all rosters
- `GET /api/rosters/{agentId}/week/{week}` – lineup for a specific week

#### 3b. Standings & Schedule

**Database tables:**
- `Schedule` – weekly matchups (agent A vs agent B, week number)
- `MatchupResults` – weekly results (team points, winner)
- `Standings` – derived from matchup results (W/L/T, points for, points against, streak)

**MCP tools:**
- `GetStandings()` – current league standings
- `GetMyMatchup(agentId, week)` – who am I playing this week?
- `GetWeekResults(week)` – all matchup results for a week

**REST endpoints:**
- `GET /api/schedule` – full season schedule
- `GET /api/schedule/week/{week}` – matchups for a week
- `GET /api/standings` – current standings
- `GET /api/results/week/{week}` – week results

#### 3c. Draft

**Database tables:**
- `DraftOrder` – pick order for the draft
- `DraftPicks` – who picked whom, with reasoning
- `DraftState` – current round, current pick, status (not_started, in_progress, complete)

**MCP tools:**
- `GetDraftState()` – current draft status, whose turn it is
- `GetAvailablePlayers(position?, limit)` – undrafted players
- `MakeDraftPick(agentId, sleeperPlayerId, reasoning)` – draft a player (validates legality)
- `GetDraftBoard()` – all picks so far

**REST endpoints:**
- `GET /api/draft/state` – current draft status
- `GET /api/draft/board` – all picks
- `GET /api/draft/available?position=&limit=` – available players
- `POST /api/draft/pick` – make a pick (used by orchestrator if needed)

#### 3d. Decision Log (centralized)

**Database table:**
- `Decisions` – all agent decisions, centrally stored and queryable

**MCP tools:**
- `LogDecision(agentId, week, type, context, reasoning, action)` – record a decision
- `GetMyRecentDecisions(agentId, limit)` – agent retrieves its own past decisions for context

**REST endpoints:**
- `GET /api/decisions?agentId=&week=&type=&limit=` – query decisions (front-end activity feed)
- `GET /api/decisions/{agentId}` – all decisions for an agent

#### 3e. Agent Profiles (centralized)

**Database table:**
- `AgentProfiles` – team name, logo path/url, model name, strategy summary, initialized status

**MCP tools:**
- `RegisterAgent(agentId, teamName, modelName, strategySummary)` – first-launch registration
- `GetAgentProfile(agentId)` – retrieve profile
- `UpdateAgentProfile(agentId, ...)` – update profile fields

**REST endpoints:**
- `GET /api/agents` – all agent profiles (front-end team list)
- `GET /api/agents/{agentId}` – single agent profile
- `GET /api/agents/{agentId}/logo` – serve logo image

### Domain 4: Commissioner Rules (new)

The LeagueAPI enforces league rules on every write operation. Agents cannot bypass these.

**Roster rules:**
- Maximum roster size (17 players)
- Position limits (e.g., max 2 QBs, max 6 RBs)
- Valid lineup (1 QB, 2 RB, 2 WR, 1 TE, 1 FLEX, 1 K, 1 DEF)
- Can't start a player on bye week or marked inactive

**Draft rules:**
- Can only pick when it's your turn
- Can't pick an already-drafted player
- Must pick within time limit (if enforced)

**Waiver rules:**
- Priority order (inverse of standings, or rolling)
- Waiver period before free agency

**Trade rules (if implemented):**
- Both parties must agree
- Commissioner veto window (or auto-approve)

## Migration Path from PlayerDatabase

Since PlayerDatabase already exists and works, the migration is additive:

1. **Rename** the project from `PlayerDatabase` to `LeagueAPI` (update namespace, csproj, solution)
2. **Migrate** from SQL Server to PostgreSQL (swap `SqlServerPlayerCatalogStore` for Npgsql-based implementation)
3. **Keep** all existing Sleeper sync, player catalog, MCP tools, REST endpoints
4. **Add** new database tables for scores, rosters, standings, draft, decisions, agent profiles
5. **Add** Yahoo API client and score sync service
6. **Add** league state services (roster management, standings calculation, draft engine)
7. **Add** commissioner validation layer
8. **Expand** MCP tools with league-facing tools
9. **Expand** REST endpoints for front-end consumption

## Database Schema Summary

```
┌──────────────────────────────────────────────┐
│                 PostgreSQL                    │
│                                               │
│  EXISTING (migrated from SQL Server):         │
│  ├─ Players (Sleeper catalog)                │
│  ├─ SleeperSyncState                         │
│                                               │
│  NEW - Scoring:                               │
│  ├─ WeeklyPlayerStats                        │
│  ├─ WeeklyPlayerPoints                       │
│  ├─ ScoringTemplate                          │
│                                               │
│  NEW - League:                                │
│  ├─ AgentProfiles                            │
│  ├─ Rosters                                  │
│  ├─ Schedule                                 │
│  ├─ MatchupResults                           │
│  ├─ DraftOrder                               │
│  ├─ DraftPicks                               │
│  ├─ DraftState                               │
│  ├─ Decisions                                │
│  └─ Standings (or derived view)              │
└──────────────────────────────────────────────┘
```

## Sub-Tasks

| # | Task | Depends On | Notes |
|---|------|-----------|-------|
| L.1 | Rename PlayerDatabase → LeagueAPI | — | Namespace, csproj, solution, Docker |
| L.1b | Migrate from SQL Server to PostgreSQL | L.1 | Swap SqlServer driver for Npgsql, update connection strings |
| L.2 | Add Yahoo OAuth client (C#) | — | Port from test_yahoo_api.py |
| L.3 | Add scoring template and calculation service | L.2 | Define stat modifiers, compute points |
| L.4 | Add weekly stats sync from Yahoo | L.2, L.3 | Pull and store raw stats + calculated points |
| L.5 | Add AgentProfiles table and endpoints | L.1 | Registration, profile CRUD |
| L.6 | Add Rosters table and management | L.1 | Add/drop, lineup setting, validation |
| L.7 | Add Schedule and Standings | L.1 | Season schedule generation, W/L tracking |
| L.8 | Add Draft engine | L.1, L.6 | Draft state, pick validation, board |
| L.9 | Add centralized Decision log | L.1, L.5 | Store and query agent decisions |
| L.10 | Add commissioner validation layer | L.6, L.7, L.8 | Rule enforcement on all writes |
| L.11 | Expand MCP tools for league operations | L.5–L.9 | Agent-facing tools for all domains |
| L.12 | Expand REST endpoints for front-end | L.5–L.9 | Read-only endpoints for dashboard |

## Open Questions

- **Yahoo OAuth in C#**: The Python script works — port to C# using `HttpClient`, or shell out to a token helper? (Recommend native C#)
- **Scoring template**: Hardcode the standard PPR scoring, or make it configurable via DB? (Recommend configurable — store in `ScoringTemplate` table)
- **Waiver wire**: Implement for v1, or defer? (Recommend stub for v1, implement for v2)
- **Trades**: Implement for v1, or defer? (Recommend defer)
- **Real-time draft updates**: SignalR for the front-end, or polling? (Can defer — front-end can poll `/api/draft/state` initially)
