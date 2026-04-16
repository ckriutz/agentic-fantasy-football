# LeagueAPI

`LeagueAPI` is the Step 1 service for the fantasy football project. It pulls the NFL player catalog from the Sleeper API, stores it in PostgreSQL, tracks sync runs in the database, and exposes the stored data through both a normal HTTP API and an MCP endpoint.

The service is Postgres-only. You must provide a connection string via `ConnectionStrings:LeagueAPI` (or the `ConnectionStrings__LeagueAPI` environment variable) — for local development this typically points at a Postgres docker container, but any reachable Postgres host works.

## What it does

- Downloads the Sleeper NFL players payload from:
  - `https://api.sleeper.app/v1/players/nfl`
- Persists players into the `players` table in Postgres
- Records each sync attempt in the `sleeper_sync_runs` table
- Supports Yahoo OAuth bootstrap and token refresh for authenticated Fantasy Sports API access (tokens are stored in the `yahoo_oauth_state` table)
- Supports manual Yahoo weekly stat syncs into the database for later scoring and reads
- Avoids re-pulling Sleeper more than once per day unless you force a sync
- Exposes player lookup and search endpoints over HTTP
- Exposes the same player lookup capabilities plus Yahoo score reads as MCP tools on `/mcp`

## How it works

The app has three main layers:

1. **Sync layer**
   - `NightlySleeperSyncService` runs as a hosted background service.
   - It can sync once on startup and then again every day at the configured UTC hour.
   - It calls Sleeper through `SleeperApiClient`.

2. **Persistence layer**
   - `PostgresPlayerCatalogStore` persists players and sync-run metadata to Postgres.
   - `PostgresYahooAuthStateStore` persists Yahoo OAuth tokens to Postgres.

3. **Read layer**
   - The HTTP API exposes player queries backed by Postgres.
   - MCP tools expose the same read functionality at `/mcp`.

## Configuration

Configuration lives in `appsettings.json`.

### Environment variables

The service respects these environment variables (prefer these over editing config files):

**Database:**
- `ConnectionStrings__LeagueAPI` - **Required.** PostgreSQL connection string. The service fails fast on startup if this is missing or empty.

**Yahoo OAuth:**
- `YAHOO_CLIENT_ID` - OAuth client ID from your Yahoo developer app (required for Yahoo features)
- `YAHOO_CLIENT_SECRET` - OAuth client secret (required for Yahoo features)
- `YAHOO_REDIRECT_URI` - redirect URI for OAuth callback; defaults to `https://localhost:3000` if not set

**Yahoo Sync:**
- `YahooSync__DefaultGameKey` - default game key for syncs; defaults to `461` (2025 NFL) if not set
- `YahooSync__DefaultSeason` - default season for syncs; defaults to `2025` if not set
- `YahooSync__PageSize` - players per page when syncing; defaults to `25` if not set

**ASP.NET Core:**
- `ASPNETCORE_URLS` - listening URL; e.g., `http://127.0.0.1:5181`
- `ASPNETCORE_ENVIRONMENT` - `Development` or `Production`

**Example startup with env vars:**

```bash
export ConnectionStrings__LeagueAPI="postgresql://user:pass@localhost/leaguedb"
export YAHOO_CLIENT_ID="your-client-id"
export YAHOO_CLIENT_SECRET="your-client-secret"
export ASPNETCORE_URLS="http://127.0.0.1:5181"
dotnet run
```

### Sleeper sync settings

```json
"SleeperSync": {
  "Enabled": true,
  "RunOnStartup": true,
  "DailySyncHourUtc": 6,
  "PlayersEndpoint": "https://api.sleeper.app/v1/players/nfl"
}
```

### Database connection

```json
"ConnectionStrings": {
  "LeagueAPI": ""
}
```

Required. The service fails fast on startup if this is missing. Prefer setting it via the `ConnectionStrings__LeagueAPI` environment variable so it can point at any Postgres host (local docker, managed Postgres, etc.).

### Yahoo OAuth settings

```json
"YahooOAuth": {
  "ClientId": "",
  "ClientSecret": "",
  "RedirectUri": "https://localhost:3000",
  "AuthorizationUrl": "https://api.login.yahoo.com/oauth2/request_auth",
  "TokenUrl": "https://api.login.yahoo.com/oauth2/get_token",
  "FantasyApiBaseUrl": "https://fantasysports.yahooapis.com/fantasy/v2"
}
```

Yahoo OAuth tokens are persisted to the `yahoo_oauth_state` Postgres table, so the database must be reachable before running the Yahoo authorization flow.

Prefer setting `ClientId` and `ClientSecret` with environment variables:

- `YAHOO_CLIENT_ID`
- `YAHOO_CLIENT_SECRET`
- `YAHOO_REDIRECT_URI` (optional override)

### Yahoo sync settings

```json
"YahooSync": {
  "Enabled": true,
  "DefaultGameKey": "461",
  "DefaultSeason": 2025,
  "PageSize": 25
}
```

This sync path writes to the database-backed Yahoo tables, so it requires:

1. `ConnectionStrings:LeagueAPI` to be configured
2. the Yahoo migrations to be applied
3. Yahoo OAuth tokens to already be bootstrapped

## Running the project

From the `src/LeagueAPI` directory:

```bash
dotnet build
dotnet run
```

If you want to pin the listening URL:

```bash
ASPNETCORE_URLS=http://127.0.0.1:5181 dotnet run
```

## Environment variables

### Required for Yahoo sync/reads

- `YAHOO_CLIENT_ID` - Your Yahoo app's OAuth client ID. Required for Yahoo OAuth flow.
- `YAHOO_CLIENT_SECRET` - Your Yahoo app's OAuth client secret. Required for Yahoo OAuth flow.

### Optional Yahoo overrides

- `YAHOO_REDIRECT_URI` - Override the configured redirect URI for Yahoo OAuth. Defaults to `https://localhost:3000` in config.

### Optional runtime configuration

- `ASPNETCORE_URLS` - ASP.NET Core listening URL. Example: `http://127.0.0.1:5181`
- `ConnectionStrings__LeagueAPI` - PostgreSQL connection string (replaces config value if set). Required; the service fails fast without it.

### Example startup command

```bash
export YAHOO_CLIENT_ID="your-app-id"
export YAHOO_CLIENT_SECRET="your-app-secret"
export ASPNETCORE_URLS="http://127.0.0.1:5181"

dotnet run
```

## Startup behavior

When the service starts:

1. It validates that `ConnectionStrings:LeagueAPI` is configured and fails fast if it is not.
2. If `SleeperSync:Enabled` is `true`, the hosted sync service starts.
3. If `SleeperSync:RunOnStartup` is `true`, it attempts an immediate sync.
4. If a successful sync already happened today, the sync is skipped.
5. The service then waits until the next configured UTC sync hour and repeats the process.

## HTTP API

### Service info

`GET /`

Returns a small summary of the service and the key routes.

### Get player by Sleeper ID

`GET /api/players/{sleeperPlayerId}`

Only active players are returned. Inactive players respond with `404 Not Found`.

Example:

```bash
curl http://127.0.0.1:5181/api/players/4046
```

### Get player by Yahoo ID

`GET /api/players/by-yahoo/{yahooId}`

Only active players are returned. Inactive players respond with `404 Not Found`.

Example:

```bash
curl http://127.0.0.1:5181/api/players/by-yahoo/30123
```

### Search players

`GET /api/players?name=&team=&position=&limit=`

Search results only include active players.

Supported query parameters:

- `name`
- `team`
- `position`
- `limit`

Example:

```bash
curl "http://127.0.0.1:5181/api/players?name=patrick%20mahomes&limit=5"
```

### Get latest sync status

`GET /api/sync/sleeper/latest`

Example:

```bash
curl http://127.0.0.1:5181/api/sync/sleeper/latest
```

### Trigger a sync manually

`POST /api/sync/sleeper?force=true`

- `force=false` respects the once-per-day guard
- `force=true` bypasses it

Example:

```bash
curl -X POST "http://127.0.0.1:5181/api/sync/sleeper?force=true"
```

### Get latest Yahoo sync status

`GET /api/sync/yahoo/latest?gameKey=&season=&week=`

Returns the latest Yahoo sync run overall or for a specific game/season/week filter.

Example:

```bash
curl "http://127.0.0.1:5181/api/sync/yahoo/latest?gameKey=449&season=2024&week=1"
```

### Trigger a Yahoo weekly sync manually

`POST /api/sync/yahoo/weekly?week=&season=&gameKey=&force=`

- `week` is required
- `season` defaults to `YahooSync:DefaultSeason`
- `gameKey` defaults to `YahooSync:DefaultGameKey`
- `force=false` skips if the same week already synced successfully today
- `force=true` bypasses that guard

Example:

```bash
curl -X POST "http://127.0.0.1:5181/api/sync/yahoo/weekly?gameKey=449&season=2024&week=1&force=true"
```

### Get Yahoo weekly stat rows

`GET /api/yahoo/stats/{season}/{week}?position=&limit=`

Returns normalized Yahoo stat rows for that week.

Example:

```bash
curl "http://127.0.0.1:5181/api/yahoo/stats/2024/1?position=QB&limit=10"
```

### Get Yahoo weekly stats for one player

`GET /api/yahoo/stats/player/{sleeperPlayerId}/{season}/week/{week}`

Example:

```bash
curl "http://127.0.0.1:5181/api/yahoo/stats/player/4046/2024/week/1"
```

You can also query by Yahoo player ID:

`GET /api/yahoo/stats/by-yahoo/{yahooId}/{season}/week/{week}`

### Get Yahoo weekly fantasy points or leaderboard

`GET /api/yahoo/points/{season}/{week}?templateKey=&position=&limit=`

Returns weekly fantasy points for the selected scoring template. If `templateKey` is omitted, the first active scoring template is used.

Example:

```bash
curl "http://127.0.0.1:5181/api/yahoo/points/2024/1?templateKey=half-ppr&position=QB&limit=10"
```

### Get Yahoo weekly fantasy points for one player

`GET /api/yahoo/points/player/{sleeperPlayerId}/{season}/week/{week}?templateKey=`

Example:

```bash
curl "http://127.0.0.1:5181/api/yahoo/points/player/4046/2024/week/1?templateKey=half-ppr"
```

You can also query by Yahoo player ID:

`GET /api/yahoo/points/by-yahoo/{yahooId}/{season}/week/{week}?templateKey=`

### Get Yahoo season fantasy point totals for one player

`GET /api/yahoo/points/player/{sleeperPlayerId}/{season}?templateKey=`

Returns season totals plus a week-by-week breakdown.

Example:

```bash
curl "http://127.0.0.1:5181/api/yahoo/points/player/4046/2024?templateKey=half-ppr"
```

You can also query by Yahoo player ID:

`GET /api/yahoo/points/by-yahoo/{yahooId}/{season}?templateKey=`

### Get scoring templates

`GET /api/yahoo/scoring-templates?activeOnly=true`

Returns the configured scoring templates and their stat modifiers.

### Yahoo OAuth status

`GET /api/yahoo/auth/status`

Returns whether Yahoo OAuth is configured, whether tokens are present, and whether an authorization flow is pending.

### Create a Yahoo authorization URL

`POST /api/yahoo/auth/authorize-url`

Returns a Yahoo authorization URL plus the generated OAuth `state` and the configured redirect URI.

Example:

```bash
curl -X POST http://127.0.0.1:5181/api/yahoo/auth/authorize-url
```

### Exchange an authorization code or redirect URL

`POST /api/yahoo/auth/exchange`

Accepts either a direct `code` or the full `redirectUrl` copied from the browser after approval.

Example using the redirect URL:

```bash
curl -X POST http://127.0.0.1:5181/api/yahoo/auth/exchange \
  -H "Content-Type: application/json" \
  -d '{"redirectUrl":"https://localhost:3000?code=...&state=..."}'
```

### Refresh Yahoo tokens manually

`POST /api/yahoo/auth/refresh`

Refreshes the stored Yahoo access token using the saved refresh token.

### Test the Yahoo connection

`GET /api/yahoo/auth/test-connection`

Makes an authenticated Yahoo request to `game/nfl` using the stored OAuth tokens.

## Yahoo end-to-end validation

To fully test the Yahoo pipeline, you need all of these in place:

1. a real PostgreSQL connection string in `ConnectionStrings:LeagueAPI`
2. the EF migrations applied
3. Yahoo OAuth credentials configured
4. a bootstrapped Yahoo token set
5. at least one active scoring template in the database

### 1. Configure the database connection

Set `ConnectionStrings:LeagueAPI` to a real Postgres database before running Yahoo syncs or Yahoo reads.

### 2. Apply the database migrations

From `src/LeagueAPI`:

```bash
dotnet ef database update
```

### 3. Configure Yahoo OAuth

Prefer environment variables:

```bash
export YAHOO_CLIENT_ID="your-client-id"
export YAHOO_CLIENT_SECRET="your-client-secret"
export YAHOO_REDIRECT_URI="https://localhost:3000"
```

### 4. Start the API

```bash
ASPNETCORE_URLS=http://127.0.0.1:5181 dotnet run
```

### 5. Bootstrap Yahoo auth

1. Create an authorization URL:

   ```bash
   curl -X POST http://127.0.0.1:5181/api/yahoo/auth/authorize-url
   ```

2. Open the returned URL in a browser, approve access, and copy the full redirect URL.
3. Exchange the redirect URL for tokens:

   ```bash
   curl -X POST http://127.0.0.1:5181/api/yahoo/auth/exchange \
     -H "Content-Type: application/json" \
     -d '{"redirectUrl":"https://localhost:3000?code=...&state=..."}'
   ```

4. Verify the token works:

   ```bash
   curl http://127.0.0.1:5181/api/yahoo/auth/test-connection
   ```

### 6. Seed an active scoring template

The Yahoo stat sync does not seed scoring templates yet. Without at least one active template, stat syncs will still work, but point endpoints will return no scored rows.

Example SQL for a simple `half-ppr` template:

```sql
INSERT INTO scoring_templates ("TemplateKey", "Name", "Description", "IsActive", "UpdatedAtUtc")
VALUES ('half-ppr', 'Half PPR', 'Baseline half-PPR scoring.', TRUE, NOW())
ON CONFLICT ("TemplateKey") DO UPDATE
SET "Name" = EXCLUDED."Name",
    "Description" = EXCLUDED."Description",
    "IsActive" = EXCLUDED."IsActive",
    "UpdatedAtUtc" = EXCLUDED."UpdatedAtUtc";

INSERT INTO scoring_template_rules ("TemplateKey", "StatId", "StatName", "Modifier")
VALUES
  ('half-ppr', 4, 'Passing Yards', 0.04),
  ('half-ppr', 5, 'Passing Touchdowns', 4),
  ('half-ppr', 6, 'Interceptions', -1),
  ('half-ppr', 9, 'Rushing Yards', 0.1),
  ('half-ppr', 10, 'Rushing Touchdowns', 6),
  ('half-ppr', 11, 'Receptions', 0.5),
  ('half-ppr', 12, 'Receiving Yards', 0.1),
  ('half-ppr', 13, 'Receiving Touchdowns', 6)
ON CONFLICT ("TemplateKey", "StatId") DO UPDATE
SET "StatName" = EXCLUDED."StatName",
    "Modifier" = EXCLUDED."Modifier";
```

### 7. Run a Yahoo weekly sync

Use a completed week so Yahoo has final stats. Example for 2024 week 1:

```bash
curl -X POST "http://127.0.0.1:5181/api/sync/yahoo/weekly?gameKey=449&season=2024&week=1&force=true"
```

### 8. Validate sync status

```bash
curl "http://127.0.0.1:5181/api/sync/yahoo/latest?gameKey=449&season=2024&week=1"
```

You want to see:

- `Status = "Succeeded"`
- `RecordCount > 0`
- `MatchedPlayerCount > 0`

### 9. Validate raw stat reads

```bash
curl "http://127.0.0.1:5181/api/yahoo/stats/2024/1?position=QB&limit=5"
curl "http://127.0.0.1:5181/api/yahoo/stats/player/4046/2024/week/1"
```

You want to see normalized stat rows like stat `4`, `5`, `6`, `9`, `10`, `11`, `12`, and `13` when Yahoo reported them for that player/week.

### 10. Validate scored point reads

```bash
curl "http://127.0.0.1:5181/api/yahoo/points/2024/1?templateKey=half-ppr&position=QB&limit=5"
curl "http://127.0.0.1:5181/api/yahoo/points/player/4046/2024/week/1?templateKey=half-ppr"
curl "http://127.0.0.1:5181/api/yahoo/points/player/4046/2024?templateKey=half-ppr"
curl "http://127.0.0.1:5181/api/yahoo/scoring-templates?activeOnly=true"
```

You want to see:

- scored rows returned for the active template
- `Breakdown` values present on point responses
- season totals adding up from weekly rows
- the active scoring template and rules returned by `/api/yahoo/scoring-templates`

### 11. Validate MCP reads

Use your MCP client against `POST /mcp` and call:

- `GetPlayerWeeklyStats`
- `GetPlayerWeeklyPoints`
- `GetTopScorersByWeek`
- `GetPlayerSeasonPoints`
- `GetScoringTemplates`
- `GetLatestYahooSyncStatus`

### Expected current limitations

- Yahoo OAuth bootstrap is still manual copy/paste of the redirect URL.
- Scoring templates must currently be inserted manually.

## MCP endpoint

The project also exposes an HTTP MCP transport at:

- `POST /mcp`

Current tools:

- `GetPlayerBySleeperId`
- `GetPlayerByYahooId`
- `SearchPlayers`
- `GetLatestSleeperSyncStatus`
- `GetLatestSportsDataSyncStatus`
- `GetPlayerWeeklyStats`
- `GetPlayerWeeklyPoints`
- `GetTopScorersByWeek`
- `GetPlayerSeasonPoints`
- `GetScoringTemplates`
- `GetLatestYahooSyncStatus`

This route is meant for MCP clients, not normal browser navigation. A plain `GET /mcp` is expected to return `405 Method Not Allowed`.

## Important implementation notes

- Player reads are based on **stored data** in Postgres, not live calls to Sleeper.
- The once-per-day guard is based on the last successful sync date in UTC, read from `sleeper_sync_runs`.
- The full raw Sleeper payload for each player is preserved on the `players.RawJson` column so later ingestion changes can be made without losing the original data.
- The model already includes `yahoo_id`, so cross-provider lookups are supported when Sleeper includes the mapping.
- Yahoo OAuth tokens are stored in the `yahoo_oauth_state` Postgres table.
- Yahoo weekly sync stores one raw player payload per player/week plus normalized stat rows and recalculated points for every active scoring template.
- Yahoo read endpoints intentionally return normalized stat and point data, not the stored `RawJson` audit payload.

## Current status

The service is Postgres-backed. To use it:

1. Have a Postgres database reachable (locally, the repo's `docker-compose.yml` starts one).
2. Set `ConnectionStrings__LeagueAPI` to point at it.
3. Run `dotnet ef database update` from `src/LeagueAPI` to apply migrations.
4. Start the service. The Sleeper sync will populate the `players` table on first run.
5. Query the HTTP API or MCP tools.
