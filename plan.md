# This is the Agentic Fantasy Football leauge.
The goal is to create a system where AI agents will autonomously play fantasy football live. As this is occuring, I will grab key events and decisions and write about them for fun.

### Key aspects:
- There will be 10 agents, all with differnet backing LLM's from OpenRouter.
- Each agent, when first launched, will define it's own strategy, create it's own team name, and then generate it's own logo. These details will be stored somewhere.
- The Sleeper API will be used to gather all the players. This information will be stored in a database.
- Weekly scores will be retrieved from the Yahoo API, this will give us player points.
- A draft will be conducted, and 17 players will be drafted by each team. The agents will draft players autonomously based on their own strategy.
- Each agent will be given access to sonar as part of perplexity for search, allowing them to keep up with player news to help make decisions.
- There may be an API available that will allow me to see player news and updates, and the agents will have access to this as well. Also, if there is a player injury, we want to get that info as well.
- If I player health status changes, the agents should be able to react to that and make decisions accordingly. So when we do a player status update, as the database is updated we can see if that player is owned by an agent, and if so we can trigger that agent to re-evaluate their roster and make any necessary changes. This may mean we need to run some sort of sync more often than nightly.
- I will want to simulate the 2025 season as best as I can, over and over again to make sure things work.
- Decisions will be added to a decision log that will allow me to coumb though for interesting things to write about. The agents will need a tool for this logging and lilely some database work as well.
- I should create some sort of casual front-end so I can see what's going on.

### Step 1 - Create the players database from sleeper API
✅ This is complete.

The player database is the foundation for everything else. It lives in the `src/LeagueAPI/` project — a .NET ASP.NET Core minimal API that also runs as an MCP server.

#### Data sources

**Sleeper API** — the primary source for NFL player data.
- Endpoint: `https://api.sleeper.app/v1/players/nfl`
- Returns a large JSON blob of all NFL players. We fetch this and persist it to PostgreSQL.
- Each player has a `FantasyDataId` field that links to SportsDataIO.

**SportsDataIO** — enriches Sleeper players with fantasy-relevant metrics.
- Endpoint: `FantasyPlayers` (requires API key, stored as environment variable — never committed).
- Key enrichment fields copied onto each player: **AverageDraftPosition**, **ByeWeek**, **LastSeasonFantasyPoints**, **ProjectedFantasyPoints**, **AuctionValue**.
- Linkage: `players.FantasyDataId == sportsdata_fantasy_players.SportsDataPlayerId`. No hard FK — if a Sleeper player has no `FantasyDataId`, enrichment fields stay null.
- SportsData records also live in their own table (`sportsdata_fantasy_players`) with full raw JSON for future use.
- True "last week fantasy points" is **out of scope** here and comes from Yahoo integration (Step 2).

#### Database design (PostgreSQL)

PostgreSQL was chosen over SQL Server because it runs natively on ARM (MacBook M2, Raspberry Pi) without emulation. The .NET ecosystem supports it well via Npgsql / EF Core.

Tables:
- `players` — the unified player table. Contains Sleeper fields plus the 5 SportsData enrichment columns (nullable).
- `sleeper_sync_runs` — tracks each Sleeper sync (start time, status, record count, errors).
- `sportsdata_fantasy_players` — raw SportsData records keyed by `SportsDataPlayerId`.
- `sportsdata_sync_runs` — tracks each SportsData sync.

Key indexes: `ByeWeek`, `FantasyDataId`, `Team + Position`.

#### Architecture: Entity vs Record

- **`PlayerEntity`** is the EF Core database row — maps directly to the `players` table.
- **`PlayerRecord`** is the API/MCP-facing DTO — what gets serialized and returned to callers.
- Both have the same 5 SportsData enrichment fields. This separation keeps the DB schema decoupled from the public contract.

#### Sync services

Both syncs follow the same pattern: API client fetches raw JSON → sync service deserializes and upserts → sync run is recorded.

- **`SleeperApiClient`** / **`SleeperPlayerSyncService`** — fetches Sleeper players, upserts into `players` table.
- **`SportsDataApiClient`** / **`SportsDataPlayerSyncService`** — fetches SportsData, upserts into `sportsdata_fantasy_players`, then cross-references by `FantasyDataId` to copy the 5 enrichment fields onto matching `players` rows.
- **`NightlySleeperSyncService`** / **`NightlySportsDataSyncService`** — `BackgroundService` implementations that run on a configurable UTC hour. Both support `RunOnStartup` and `Enabled` flags via options classes.
- SportsData sync is configured via `SportsDataSyncOptions` (`Enabled`, `BaseUrl`, `FantasyPlayersEndpoint`, `DailySyncHourUtc`, `RunOnStartup`, `ApiKey`).

#### API surface (REST)

```
GET  /api/players/{sleeperPlayerId}         — get a single player by Sleeper ID
GET  /api/players/by-yahoo/{yahooId}        — get a single player by Yahoo ID
GET  /api/players?name=&team=&position=     — search/filter players
      &byeWeek=&minProjectedPoints=
      &maxAverageDraftPosition=
      &sortBy=&sortDescending=&limit=
GET  /api/sync/sleeper/latest               — latest Sleeper sync status
POST /api/sync/sleeper?force=true           — trigger a Sleeper sync
GET  /api/sync/sportsdata/latest            — latest SportsData sync status
```

Sort options for `sortBy`: `projectedPoints`, `adp`, `lastSeasonPoints`, `auctionValue`, `name` (default).

#### MCP tools (for agents)

```
GetPlayerBySleeperId(sleeperPlayerId)
GetPlayerByYahooId(yahooId)
SearchPlayers(name, team, position, byeWeek, minProjectedPoints,
              maxAverageDraftPosition, sortBy, sortDescending, limit)
GetLatestSleeperSyncStatus()
GetLatestSportsDataSyncStatus()
```

All player responses include SportsData enrichment fields automatically (nullable, backward-compatible). There are no separate SportsData-specific player endpoints — agents query one unified player surface.

### Step 2 - Create the Yahoo API connection for scores
✅ This is complete.

Yahoo is used as a provider of weekly player-level stat data that feeds this project's own scoring engine. The project does **not** sync or manage a Yahoo fantasy league — Yahoo is purely a data source. All Yahoo integration lives in `src/LeagueAPI/` alongside the existing Sleeper and SportsData providers.

#### Design decisions

- Yahoo is accessed through OAuth 2.0 (authorization code flow).
- The system ingests raw weekly player stats and calculates fantasy points locally using configurable scoring templates, rather than depending on Yahoo's league-specific scoring.
- `PlayerEntity.YahooId` is the primary join key from Yahoo data to the canonical Sleeper-based player catalog.
- Unmatched Yahoo players (no matching `YahooId` in the Sleeper catalog) are logged and preserved — never silently discarded.

#### OAuth authentication

Yahoo requires OAuth 2.0. The flow has two phases:

- **Bootstrap auth** (one-time, manual): Generate an authorization URL, approve in a browser, then exchange the redirect URL for access + refresh tokens. This is interactive — the user must copy-paste the redirect URL.
- **Runtime auth** (automatic): The system automatically refreshes tokens before they expire. Tokens are persisted to PostgreSQL (`yahoo_oauth_state` table) so they survive restarts.

The implementation lives in `YahooOAuthService.cs` and `PostgresYahooAuthStateStore.cs`. Thread-safe via semaphore locking.

Auth endpoints:

```
POST /api/yahoo/auth/authorize-url    — generate OAuth URL to open in browser
POST /api/yahoo/auth/exchange         — exchange code or redirect URL for tokens
POST /api/yahoo/auth/refresh          — manual token refresh
GET  /api/yahoo/auth/status           — check token state and expiry
GET  /api/yahoo/auth/test-connection  — verify token works against Yahoo API
```

Configuration via environment variables (never committed):

```bash
export YAHOO_CLIENT_ID="your-client-id"
export YAHOO_CLIENT_SECRET="your-client-secret"
export YAHOO_REDIRECT_URI="https://localhost:3000"
```

Or via `appsettings.json` under the `YahooOAuth` section.

#### Database design

Yahoo adds the following tables to the PostgreSQL database:

- `yahoo_oauth_state` — singleton row (ID=1) storing access token, refresh token, expiry, scope, last refreshed timestamp.
- `yahoo_sync_runs` — tracks each Yahoo sync (game key, season, week, status, record count, matched/unmatched player counts, error message, timestamps).
- `weekly_player_stats` — one row per player per week. Contains Yahoo player ID, matched Sleeper player ID (nullable), player name, team, position, season, week, and `RawJson` audit column preserving the original Yahoo payload.
- `weekly_player_stat_values` — normalized child rows of `weekly_player_stats`. Each row is a single stat (stat ID, stat name, decimal value) for a player-week.
- `weekly_player_points` — calculated fantasy points per player/week/scoring template, with a breakdown JSON showing per-rule contributions.
- `scoring_templates` — defines scoring rule sets (template key, name, description, active flag).
- `scoring_template_rules` — individual stat modifiers per template (stat ID, stat name, multiplier).

Key indexes on (GameKey, Season, Week), (Season, Week, YahooPlayerId), and (TemplateKey).

#### Sync pipeline

The sync fetches weekly player stats from Yahoo and writes them to the database. It follows the same pattern as Sleeper and SportsData syncs.

Flow: `YahooFantasyApiClient` fetches paginated JSON → `YahooPlayerSyncService` parses and upserts stats → `ScoringService` recalculates points for all active templates → sync run is recorded.

Key behaviors:
- Paginates through Yahoo API results (configurable page size, default 25).
- Parses Yahoo's nested JSON with recursive node traversal. Maps 85+ stat IDs to names.
- Cross-references Yahoo player IDs to Sleeper player IDs via `PlayerEntity.YahooId`.
- Idempotent upserts — safe to re-run for the same week.
- Once-per-day guard: skips re-syncing the same week if already synced today (unless `force=true`).
- Concurrency-safe via `SemaphoreSlim`.

Manual sync endpoint:

```
POST /api/sync/yahoo/weekly?week=1&season=2025&gameKey=461&force=true
GET  /api/sync/yahoo/latest?gameKey=461&season=2025&week=1
```

All parameters are optional and fall back to defaults from `YahooSync` config.

#### Nightly scheduled sync

`NightlyYahooSyncService` is a `BackgroundService` that runs the Yahoo sync automatically on a daily schedule, following the same pattern as `NightlySleeperSyncService` and `NightlySportsDataSyncService`.

Configuration (`appsettings.json` under `YahooSync`):

| Setting | Default | Description |
|---------|---------|-------------|
| `Enabled` | `true` | Master enable/disable for Yahoo sync |
| `RunOnStartup` | `false` | Run a sync immediately when the service starts |
| `DailySyncHourUtc` | `6` | Hour (0–23 UTC) to run the nightly sync |
| `DefaultGameKey` | `"461"` | Yahoo game key for the target season |
| `DefaultSeason` | `2025` | NFL season year |
| `DefaultWeek` | `1` | Which week to sync (update as season progresses) |
| `PageSize` | `25` | Number of players per Yahoo API page |

**Important**: `DefaultWeek` must be updated manually as the season progresses. The nightly service syncs the configured week each day.

#### Scoring system

Fantasy points are calculated locally, not sourced from Yahoo. The `ScoringService` multiplies each stat value by the corresponding rule modifier from the active scoring template(s), then stores the result with a per-rule breakdown.

Scoring templates must currently be seeded manually via SQL. Example full-PPR template:

```sql
INSERT INTO scoring_templates ("TemplateKey", "Name", "Description", "IsActive", "UpdatedAtUtc")
VALUES ('full-ppr', 'Full PPR', 'Baseline full-PPR scoring.', TRUE, NOW())
ON CONFLICT ("TemplateKey") DO UPDATE
SET "Name" = EXCLUDED."Name",
    "Description" = EXCLUDED."Description",
    "IsActive" = EXCLUDED."IsActive",
    "UpdatedAtUtc" = EXCLUDED."UpdatedAtUtc";

INSERT INTO scoring_template_rules ("TemplateKey", "StatId", "StatName", "Modifier")
VALUES
  ('full-ppr', 4,  'Passing Yards',              0.04),
  ('full-ppr', 5,  'Passing Touchdowns',          4),
  ('full-ppr', 6,  'Interceptions',               -1),
  ('full-ppr', 9,  'Rushing Yards',               0.1),
  ('full-ppr', 10, 'Rushing Touchdowns',          6),
  ('full-ppr', 11, 'Receptions',                  1.0),
  ('full-ppr', 12, 'Receiving Yards',             0.1),
  ('full-ppr', 13, 'Receiving Touchdowns',        6),
  ('full-ppr', 15, '2-Point Conversion (Pass)',   2),
  ('full-ppr', 16, '2-Point Conversion (Rush)',   2),
  ('full-ppr', 17, 'Fumbles Lost',                -1),
  ('full-ppr', 19, '2-Point Conversion (Rec)',    2),
  ('full-ppr', 57, 'PAT Made',                   1),
  ('full-ppr', 58, 'PAT Missed',                 -1),
  ('full-ppr', 59, 'FG Made 0-19 Yards',         3),
  ('full-ppr', 60, 'FG Made 20-29 Yards',        3),
  ('full-ppr', 61, 'FG Made 30-39 Yards',        3),
  ('full-ppr', 62, 'FG Made 40-49 Yards',        4),
  ('full-ppr', 63, 'FG Made 50+ Yards',          5),
  ('full-ppr', 64, 'FG Missed 0-19 Yards',       -2),
  ('full-ppr', 65, 'FG Missed 20-29 Yards',      0),
  ('full-ppr', 66, 'FG Missed 30-39 Yards',      0),
  ('full-ppr', 67, 'FG Missed 40-49 Yards',      0),
  ('full-ppr', 68, 'FG Missed 50+ Yards',        0),
  ('full-ppr', 45, 'Sack',                       1),
  ('full-ppr', 46, 'Defensive Interception',     2),
  ('full-ppr', 47, 'Fumble Recovery',            2),
  ('full-ppr', 48, 'Defensive/ST Touchdown',     6),
  ('full-ppr', 49, 'Safety',                     2),
  ('full-ppr', 50, 'Blocked Kick',               2),
  ('full-ppr', 52, 'Points Allowed 0',           10),
  ('full-ppr', 53, 'Points Allowed 1-6',         7),
  ('full-ppr', 54, 'Points Allowed 7-13',        4),
  ('full-ppr', 55, 'Points Allowed 14-20',       1),
  ('full-ppr', 56, 'Points Allowed 21-27',       0),
  ('full-ppr', 57, 'Points Allowed 28-34',       -1),
  ('full-ppr', 58, 'Points Allowed 35+',         -4)
ON CONFLICT ("TemplateKey", "StatId") DO UPDATE
SET "StatName" = EXCLUDED."StatName",
    "Modifier" = EXCLUDED."Modifier";
```

#### API surface (REST)

Raw stats:
```
GET /api/yahoo/stats/{season}/{week}?position=&limit=
GET /api/yahoo/stats/player/{sleeperPlayerId}/{season}/week/{week}
GET /api/yahoo/stats/by-yahoo/{yahooId}/{season}/week/{week}
```

Scored points:
```
GET /api/yahoo/points/{season}/{week}?templateKey=&position=&limit=
GET /api/yahoo/points/player/{sleeperPlayerId}/{season}/week/{week}?templateKey=
GET /api/yahoo/points/by-yahoo/{yahooId}/{season}/week/{week}?templateKey=
GET /api/yahoo/points/player/{sleeperPlayerId}/{season}?templateKey=
GET /api/yahoo/points/by-yahoo/{yahooId}/{season}?templateKey=
```

Templates and sync:
```
GET  /api/yahoo/scoring-templates?activeOnly=true
POST /api/sync/yahoo/weekly?week=&season=&gameKey=&force=
GET  /api/sync/yahoo/latest?gameKey=&season=&week=
```

League info:
```
GET /api/yahoo/league/{leagueKey}/settings/raw
```

Template key auto-resolves to the first active template if omitted. Position filtering and configurable limits are supported on leaderboard endpoints.

#### MCP tools (for agents)

```
GetPlayerWeeklyStats(yahooId, season, week)
GetPlayerWeeklyPoints(yahooId, season, week, templateKey?)
GetTopScorersByWeek(season, week, templateKey?, position?, limit)
GetPlayerSeasonPoints(yahooId, season, templateKey?)
GetScoringTemplates(activeOnly)
GetLatestYahooSyncStatus(gameKey?, season?, week?)
```

#### Validation steps

To fully test the Yahoo pipeline end-to-end:

1. Set `ConnectionStrings:LeagueAPI` to a real Postgres database.
2. Run migrations: `dotnet ef database update` from `src/LeagueAPI`.
3. Configure Yahoo OAuth credentials (environment variables or appsettings).
4. Start the API: `ASPNETCORE_URLS=http://127.0.0.1:5181 dotnet run`
5. Bootstrap Yahoo auth:
   - `POST /api/yahoo/auth/authorize-url` → open URL in browser → approve
   - `POST /api/yahoo/auth/exchange` with the redirect URL
   - `GET /api/yahoo/auth/test-connection` to verify
6. Seed at least one active scoring template (SQL above).
7. Trigger a sync: `POST /api/sync/yahoo/weekly?week=1&season=2024&gameKey=449&force=true`
8. Check sync status: `GET /api/sync/yahoo/latest?gameKey=449&season=2024&week=1` — expect `Status = "Succeeded"`, `RecordCount > 0`, `MatchedPlayerCount > 0`.
9. Verify raw stats: `GET /api/yahoo/stats/2024/1?position=QB&limit=5`
10. Verify scored points: `GET /api/yahoo/points/2024/1?templateKey=full-ppr&position=QB&limit=5`
11. Verify MCP tools respond correctly.

#### Known limitations

- Yahoo OAuth bootstrap is manual (copy-paste redirect URL from browser). Future improvement: add a callback endpoint for headless/Docker deployments.
- Scoring templates must be inserted via SQL — no auto-seeding or admin API yet.
- `DefaultWeek` in config must be updated manually as the season progresses.
- If no active scoring template exists, stat syncs succeed but point reads return empty.

#### Risks and mitigations

- **Yahoo data availability**: Yahoo may not expose all desired stats outside league-specific context. Mitigation: store raw payloads for parser iteration, calculate points locally.
- **Auth expiry**: If refresh tokens expire or are revoked, bootstrap auth must be repeated. Mitigation: tokens are persisted durably, runtime refresh is automatic.
- **Player mapping gaps**: Some Yahoo players may not have matching `YahooId` in the Sleeper catalog. Mitigation: unmatched rows are logged and preserved in sync metadata.

#### File-level implementation map

- `Configuration/YahooOAuthOptions.cs` — OAuth settings
- `Configuration/YahooSyncOptions.cs` — Sync settings (enable, defaults, schedule)
- `Services/YahooOAuthService.cs` — OAuth flow (authorize, exchange, refresh)
- `Services/PostgresYahooAuthStateStore.cs` — Token persistence
- `Services/YahooFantasyApiClient.cs` — Authenticated Yahoo API calls
- `Services/YahooPlayerSyncService.cs` — Sync orchestration, JSON parsing, upserts
- `Services/YahooReadService.cs` — All read queries (stats, points, templates)
- `Services/ScoringService.cs` — Points calculation engine
- `HostedServices/NightlyYahooSyncService.cs` — Scheduled daily sync
- `Models/` — Yahoo entities (`YahooSyncRun`, `WeeklyPlayerStat`, `WeeklyPlayerStatValue`, `WeeklyPlayerPoint`, `ScoringTemplate`, `ScoringTemplateRule`, `YahooOAuthStateEntity`) and DTOs
- `Tools/YahooReadTools.cs` — 6 MCP tools
- `Migrations/` — Schema migrations for all Yahoo tables
- `Program.cs` — Service registration, HttpClient factories, route mapping

### Step 3 - Create the agents
✅ This is complete.
We will create 10 agents, each with a different backing LLM from OpenRouter. Each agent will have its own strategy for drafting players and making decisions throughout the season. We will need to define the strategies for each agent, and then implement those strategies in code. Each agent will need to be able to access the player database and the scores database in order to make informed decisions.

Right now, we assume the agents will all be inside the same project, and the main program will trigger them to do things either by event or time driven, or both.

The first time an agent is created, it needs to check to see if it has a strategy, team name, and logo. If it doesn't, it needs to create those things and save them to the database. Once that occurs, we need to ensure this check doesn't occur again.

It needs to be able to have some sort of memory, so it can remember past decisions and outcomes. This will allow it to learn and adapt its strategy over time. This might be done in MD files, or something more exciting like Mem0, though this would be more expensive. We will see how it goes.

Each agent needs to have the following capabilities:
- Define its own strategy for drafting players and making decisions.
- Create its own team name and logo.
- Access it's own list of players.
- Get information about player status through search.
- Access the player database to get information about players.
- Access the scores database to get information about player points.
- Make decisions based on its strategy and the information it has access to.
- When it makes a decision, it should log that decision in a decision log for later analysis.

This is mostly done. The agents are created and they define their own strategy, team name, and logo. They also have access to the player database and the scores database. They can make decisions based on their strategy and the information they have access to. They also log their decisions in a decision log for later analysis.

** Real qucik, this is how the decisions API works:
```
GET /api/decisions is already there and supports these query params:

 - ?agentId= — filter by agent
 - ?type= — filter by decision type
 - ?week= — filter by week
 - ?limit= — results to return (default 50, max 200)

Results come back ordered by CreatedAtUtc descending (newest first). Give it a try!
```

### Step 4 - Create the League System
This is where we will create the system that allows the agents to play against each other. We will need to:

- Create a schedule for the season, and then have the agents play their games according to that schedule.
- Calculate the points for each player and team, and then determine the winner of each game.
- Keep track of the standings throughout the season.
- Track key events and decisions made by the agents, and log those for later analysis and writing.
- Keep track of what players belong to which teams.

### Step 5 - Wire up the Draft
The draft is a key part of the fantasy football season, and we will need to create a system that allows the agents to draft players autonomously. We will need to create a draft order, and then have the agents take turns drafting players according to that order. The agents will need to use their strategies to determine which players to draft, and they will need to access the player database to get information about the players they are considering drafting. We will also need to keep track of which players have been drafted and which players are still available.

This has been completed but some testing needs to be done. If we stop a draft in the middle, can we restart and draft again? This needs to be tested.


### Step 6 - Mock a season using 2025 data
Once we have all the pieces in place, we will want to simulate the 2025 season using the data we have collected. This will allow us to see how the agents perform against each other, and it will also allow us to identify any issues or bugs in the system. We will want to run multiple simulations of the season to see how the agents perform under different conditions. We will want to analyze the results of the simulations to see if there are any interesting patterns or insights that we can write about. We will also want to use the decision logs to analyze the decisions made by the agents and see if there are any interesting trends or patterns in their decision-making processes.

### Step 7 - Create a front-end to visualize the league
To make it easier to see what's going on in the league, we will want to create a front-end that allows us to visualize the teams, players, scores, and standings. This could be a web application that displays the information in a user-friendly way. We could use a framework like React to build the front-end, and we could use a library like D3.js to create visualizations of the data. The front-end should allow us to see the teams and their players, the scores for each week, and the overall standings in the league. We could also include features that

Stuff that I still need to do:
- Determine the best location for external prompt/context files for FantasyAgent (for example: content files in the project vs embedded resources), balancing editability during development with reliability in published builds.

# Some things to add, change, or refactor:
- We may want to create a BootstrapService that handles all the bootstrapping logic for the agents, including strategy definition, team name/logo creation, and initial player research. This would help keep the main program cleaner and more focused on orchestration.
- As part of the BootstrapService, I want to find a way to read in the profile.json file to see if the agent has already been bootstrapped, and if so, skip the bootstrapping process. This will save us on tokens.
- Lets move the bootstrap.md, and profile.md files out of the Agents folder. This will help keep the Agents folder cleaner and more focused on the agent code itself. We can create a new folder called "AgentData" or something similar to store these files. Can we store them in Azure Blob Storage or something like that? This would allow us to easily access and update the files without having to worry about file paths and permissions on different machines.
- When the agents are bootstrapped, we need to save their logos locally or in blob storage as well. This will allow us to easily access and display the logos in the front-end. Those logos do not last long there.
- When the search tool is used, I want to log which agent used the tool, what they searched for, and what results they got back. This will allow us to analyze how the agents are using the search tool and see if there are any interesting patterns or trends in their search behavior. Also, I want to log the tokens used in the process.
- I might want to see how the draft went, so adding the pick information to the DraftRunner and the draft-state.json file would be helpful. This would allow us to see which players were drafted by which teams, and in which order. I could potentially match this to the decison log as well to see which agent made which pick and what their reasoning was at the time. This would be really interesting to analyze and write about.

### Runtime Notes:
April 24th - Using the new deepseek/deepseek-v4-flash model fails bootstrapping. Falling back on the deepseek/deepseek-v3.2 model, which works.
April 24th - A Full bootstrap of 10 players costs about $0.60, which isn't bad.
April 24th - arcee-ai/trinity-large-thinking didnt select a player and that makes me think this model is not qualified to play. Going to swap it out.
April 24th - xiaomi/mimo-v2.5 us also failing bootstrapping, swapping out with 
April 24th - Starting credits before a test daft: $13.82.