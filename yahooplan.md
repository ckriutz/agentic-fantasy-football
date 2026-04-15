# Yahoo integration plan

## Objective

Add a Yahoo-backed data pipeline to this project so the system can authenticate with Yahoo, pull weekly player data, store that data in `LeagueAPI`, and expose a normalized read surface that the fantasy league engine and agents can use for this project's own scoring rules.

This plan assumes:

- Yahoo will be accessed through OAuth.
- The project is **not** syncing or managing a Yahoo fantasy league.
- Yahoo is being used as a provider of reusable player-level data that feeds a custom scoring engine.
- `LeagueAPI` is the right place for the integration because it already owns data ingestion, persistence, and read APIs.

## Current state analysis

### Repository roadmap

The repo-level `plan.md` already places Yahoo after the player catalog work and describes it as the source for weekly score data.

### Current backend shape

`src/LeagueAPI` is already the active integration service and has a repeatable pattern that Yahoo should follow:

- configuration classes under `Configuration/`
- named `HttpClient` registrations in `Program.cs`
- provider-specific clients under `Services/`
- provider-specific sync services and nightly hosted services
- EF Core persistence through `LeagueApiDbContext`
- HTTP endpoints and MCP tools for reads

This is important because Yahoo should be added as another provider subsystem, not as ad hoc logic inside `AgenticLeague`.

### Existing player identity mapping

`PlayerEntity` already contains `YahooId`, which is the strongest current bridge between the canonical Sleeper-based player catalog and Yahoo data. That means the Yahoo work should normalize into the existing player table instead of creating a second disconnected player directory.

### Existing proof of concept

`test_yahoo_api.py` already demonstrates the essential Yahoo auth flow:

- authorization-code flow
- redirect handling by copying the returned URL
- token exchange
- refresh-token usage
- bearer-token API requests
- durable local token storage

That script should be treated as the behavioral reference for the .NET implementation.

### Current project maturity

`src/AgenticLeague` is still mostly scaffolding, while `LeagueAPI` already contains the sync and storage patterns. The fastest safe path is:

1. build Yahoo support in `LeagueAPI`
2. expose normalized reads
3. let later agent and league work consume those reads

## Planning decision

The integration should target **player-level weekly Yahoo data for custom scoring**, not Yahoo-league synchronization.

That changes the design in an important way:

- we should not build league/team sync first
- we should not model Yahoo as the source of truth for standings, rosters, or matchups
- we should prefer storing raw or normalized weekly player data that can be scored locally

## Proposed architecture

### 1. Yahoo configuration layer

Add a Yahoo-specific configuration section in `LeagueAPI` for:

- client ID
- client secret
- redirect URI
- auth base URL
- token URL
- fantasy API base URL
- sync enable/disable
- run on startup
- target season or game key if needed
- token persistence settings

This should mirror the existing `SleeperSyncOptions` and `SportsDataSyncOptions` style so the service remains consistent.

### 2. OAuth and token management

Add a Yahoo auth/token component responsible for:

- generating the authorization URL
- exchanging the auth code for access + refresh tokens
- refreshing tokens before or when requests fail
- persisting current token state durably
- exposing a valid bearer token to the Yahoo API client

Because the current proof of concept is interactive, the .NET design should explicitly separate:

- **bootstrap auth**: one-time or occasional manual authorization to obtain the first token set
- **runtime auth**: automatic refresh and reuse during normal sync runs

### 3. Yahoo API client

Add a provider client that:

- accepts a valid bearer token
- makes authenticated Yahoo API calls
- isolates endpoint construction from sync logic
- returns raw payloads or typed models for the selected Yahoo resources

The client should be narrow and provider-specific, following the same role split as `SleeperApiClient` and `SportsDataApiClient`.

### 4. Persistence model

Extend EF Core to persist Yahoo-specific data. At minimum the design should include:

- `yahoo_sync_runs`
  - sync run ID
  - started/completed timestamps
  - status
  - row count
  - error message
- `yahoo_auth_state`
  - token type
  - access token
  - refresh token
  - expires at
  - last refreshed at
  - any required Yahoo metadata
- `yahoo_player_weekly_data`
  - Yahoo player ID
  - season
  - week
  - provider payload
  - normalized values needed for local scoring
  - updated timestamp

Possible extension:

- `yahoo_game_metadata`
  - game key
  - season
  - league-independent metadata if Yahoo requires it for requests

The main rule is that Yahoo tables should store enough information to:

1. re-run or inspect syncs
2. refresh auth safely
3. compute project-specific scoring without re-calling Yahoo for every read

### 5. Player normalization strategy

Use `PlayerEntity.YahooId` as the primary join key from Yahoo data to the canonical player catalog.

Planned behavior:

- store Yahoo rows keyed by Yahoo player ID and week
- join to canonical players through `YahooId`
- expose reads by Sleeper player ID and Yahoo ID
- keep provider payloads available for debugging and future parser changes

If Yahoo returns rows that cannot be matched to an existing `YahooId`, the sync should:

- record them as unmatched in logs and sync metadata
- avoid silently discarding mapping failures
- keep enough raw data to debug the mismatch

### 6. Normalization and scoring model

Because this project is not using a Yahoo league as the scoring authority, the safest model is:

1. ingest Yahoo weekly player data
2. normalize the stat inputs needed for scoring
3. compute fantasy points locally according to this project's rules

That avoids over-coupling the system to Yahoo's league-specific scoring behavior.

If implementation proves Yahoo only exposes usable points in league-specific contexts, the fallback should be:

- ingest the closest available raw player stats from Yahoo
- calculate points entirely inside this project

### 7. Read surface for downstream consumers

Once data is stored, add `LeagueAPI` reads for:

- weekly player data by sleeper ID
- weekly player data by Yahoo ID
- weekly score lookup for a week or season/week pair
- latest Yahoo sync status
- unmatched Yahoo rows or mapping diagnostics if useful for operations

MCP exposure is optional for the first pass, but normal HTTP reads should be part of the plan because the rest of the system will need them.

## Implementation phases

### Phase 1 - Confirm target Yahoo resource shape

Goal: identify exactly which Yahoo endpoints provide the weekly player data needed for local scoring.

Work:

- inspect the successful Yahoo resources reachable after OAuth
- determine whether player-week data can be retrieved without a Yahoo fantasy league
- identify the fields required for this project's scoring model
- decide whether the persisted normalized model is stat-based or point-based

Why this phase matters:

- it reduces the main technical risk early
- it prevents building schema around the wrong Yahoo response shape

### Phase 2 - Build auth bootstrap and token persistence

Goal: make Yahoo authentication stable in C#.

Work:

- add Yahoo options/configuration
- add a bootstrap auth path that mirrors `test_yahoo_api.py`
- add token exchange and refresh logic
- persist auth state durably
- add clear failure paths for expired/invalid tokens

Deliverable:

- a repeatable .NET auth flow that can obtain and refresh tokens without depending on the Python script

### Phase 3 - Add database schema and models

Goal: persist Yahoo auth state, sync state, and player-week data.

Work:

- add EF entities
- update `LeagueApiDbContext`
- create migrations
- add indexes on Yahoo player ID, season, week, and join fields

Deliverable:

- a schema that supports syncs, reads, and debugging

### Phase 4 - Implement Yahoo sync pipeline

Goal: fetch Yahoo data and write it into the database reliably.

Work:

- create `YahooApiClient`
- create `YahooPlayerSyncService`
- add sync-run tracking similar to the existing provider flows
- handle idempotent upserts for season/week/player rows
- store raw payload plus normalized scoring inputs

Deliverable:

- a sync path that can populate weekly Yahoo data for downstream reads

### Phase 5 - Add scheduled and manual execution paths

Goal: make the sync usable operationally.

Work:

- add a hosted background sync service if scheduled refreshes are desired
- add manual trigger endpoints for forced syncs
- add latest-sync status endpoints

Deliverable:

- the same operational ergonomics already present for Sleeper and SportsData

### Phase 6 - Expose read APIs for league and agent consumers

Goal: make Yahoo-backed data usable by the rest of the system.

Work:

- add HTTP endpoints for weekly player lookups
- optionally add MCP tools if agent workflows need direct structured access
- return normalized data shaped for later scoring and decision logic

Deliverable:

- stable read surfaces that AgenticLeague can consume later

### Phase 7 - Documentation and operational notes

Goal: make the feature runnable without rediscovering setup steps.

Work:

- document Yahoo app setup
- document required environment variables or config values
- document bootstrap auth steps
- document sync behavior and known limitations

Deliverable:

- a developer can configure Yahoo access and run the sync without reading the implementation

## File-level impact

Expected primary touch points:

- `src/LeagueAPI/Program.cs`
  - register Yahoo config, clients, services, and routes
- `src/LeagueAPI/Configuration/`
  - add Yahoo options types
- `src/LeagueAPI/Services/`
  - add OAuth/token service
  - add Yahoo API client
  - add Yahoo sync service
- `src/LeagueAPI/HostedServices/`
  - add scheduled Yahoo sync service if needed
- `src/LeagueAPI/Data/LeagueApiDbContext.cs`
  - add EF sets and model configuration
- `src/LeagueAPI/Models/`
  - add Yahoo entities and transport models
- `src/LeagueAPI/Migrations/`
  - add schema migration for Yahoo tables
- `src/LeagueAPI/README.MD`
  - document setup and usage

Secondary impact:

- possibly `src/LeagueAPI/Tools/` if MCP access is added
- later `src/AgenticLeague/` consumers, but not in the first Yahoo phase

## Data model guidance

The Yahoo ingestion layer should prefer a shape that supports both storage and later score calculation.

Recommended stored fields per player/week:

- Yahoo player ID
- season
- week
- player display name from Yahoo
- team
- position
- canonical matched Sleeper player ID if resolvable
- raw provider payload
- normalized stat fields needed for scoring
- normalized fantasy points if calculated locally
- updated timestamp

This gives flexibility if the scoring formula changes later.

## Validation strategy

The implementation should explicitly validate:

1. OAuth bootstrap works in C#
2. refresh-token flow works after initial auth
3. Yahoo requests succeed with bearer auth
4. player rows map correctly through `YahooId`
5. season/week upserts are idempotent
6. downstream reads return normalized records for scoring

The biggest validation checkpoint is confirming the Yahoo endpoint shape supports the required weekly player data without tying the project to a Yahoo-managed league.

## Risks and decisions to watch

### Main technical risk

Yahoo may not expose all desired fantasy point data outside league-specific context.

Mitigation:

- design around ingesting raw weekly player data
- calculate project scoring locally
- keep raw payloads for parser iteration

### Auth operational risk

Yahoo bootstrap auth is interactive.

Mitigation:

- treat first authorization as a setup step
- persist refresh tokens durably
- keep runtime sync non-interactive after bootstrap
- future improvement: add a callback endpoint or similar container-friendly bootstrap flow so headless Docker deployments do not rely on manually copying the full redirect URL

### Mapping risk

Some Yahoo rows may fail to map cleanly through `YahooId`.

Mitigation:

- log unmatched rows
- expose diagnostics in sync status if needed
- preserve raw unmatched payloads

## Todo list

1. **define-yahoo-scope** - Use Yahoo via OAuth, but do not sync a Yahoo fantasy league; instead, pull reusable player data that can feed this project's own scoring system.
2. **design-yahoo-schema** - Add EF models and database design for Yahoo sync runs, OAuth/token state, and weekly player stat/score storage linked to existing players through `YahooId`.
3. **build-yahoo-auth** - Recreate the proven `test_yahoo_api.py` OAuth flow in C# with app configuration, token exchange, refresh handling, and durable token storage.
4. **build-yahoo-sync** - Add Yahoo API client, sync service, and optional hosted/background sync flow that pulls the selected weekly player data into the database.
5. **expose-yahoo-reads** - Add HTTP endpoints and, if useful, MCP tools for querying weekly player scoring inputs and Yahoo sync status.
6. **document-and-validate** - Document required Yahoo app setup and verify the end-to-end flow against a real Yahoo account and target data set.

## End-to-end validation steps

To fully test the Yahoo pipeline, all of these need to be in place:

1. a real PostgreSQL connection string in `ConnectionStrings:LeagueAPI`
2. the EF migrations applied
3. Yahoo OAuth credentials configured
4. a bootstrapped Yahoo token set
5. at least one active scoring template in the database

### Validation sequence

1. Set `ConnectionStrings:LeagueAPI` to a real Postgres database.
2. From `src/LeagueAPI`, run:

   ```bash
   dotnet ef database update
   ```

3. Configure Yahoo OAuth, preferably with environment variables:

   ```bash
   export YAHOO_CLIENT_ID="your-client-id"
   export YAHOO_CLIENT_SECRET="your-client-secret"
   export YAHOO_REDIRECT_URI="https://localhost:3000"
   ```

4. Start the API:

   ```bash
   ASPNETCORE_URLS=http://127.0.0.1:5181 dotnet run
   ```

5. Bootstrap Yahoo auth:
   - `POST /api/yahoo/auth/authorize-url`
   - approve access in the browser
   - `POST /api/yahoo/auth/exchange`
   - `GET /api/yahoo/auth/test-connection`

6. Seed at least one active scoring template. Example `full-ppr` SQL:

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
     -- Offense
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
     -- Kicking
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
     -- Team Defense / Special Teams
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

7. Run a Yahoo weekly sync for a completed week, for example:

   ```bash
   curl -X POST "http://127.0.0.1:5181/api/sync/yahoo/weekly?gameKey=449&season=2024&week=1&force=true"
   ```

8. Validate sync status:

   ```bash
   curl "http://127.0.0.1:5181/api/sync/yahoo/latest?gameKey=449&season=2024&week=1"
   ```

   Expected:
   - `Status = "Succeeded"`
   - `RecordCount > 0`
   - `MatchedPlayerCount > 0`

9. Validate raw stat reads:

   ```bash
   curl "http://127.0.0.1:5181/api/yahoo/stats/2024/1?position=QB&limit=5"
   curl "http://127.0.0.1:5181/api/yahoo/stats/player/4046/2024/week/1"
   ```

10. Validate scored point reads:

   ```bash
   curl "http://127.0.0.1:5181/api/yahoo/points/2024/1?templateKey=half-ppr&position=QB&limit=5"
   curl "http://127.0.0.1:5181/api/yahoo/points/player/4046/2024/week/1?templateKey=half-ppr"
   curl "http://127.0.0.1:5181/api/yahoo/points/player/4046/2024?templateKey=half-ppr"
   curl "http://127.0.0.1:5181/api/yahoo/scoring-templates?activeOnly=true"
   ```

11. Validate the same surface through MCP:
   - `GetPlayerWeeklyStats`
   - `GetPlayerWeeklyPoints`
   - `GetTopScorersByWeek`
   - `GetPlayerSeasonPoints`
   - `GetScoringTemplates`
   - `GetLatestYahooSyncStatus`

### Important current limitations

- Yahoo OAuth bootstrap is still manual copy/paste of the redirect URL.
- Yahoo sync and Yahoo reads require the SQL-backed path; snapshot-only mode is not enough.
- Scoring templates must currently be inserted manually.
- Stat sync can succeed even if point reads stay empty when no active scoring template exists.
