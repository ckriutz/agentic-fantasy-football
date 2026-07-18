# LeagueAPI

`LeagueAPI` stores fantasy football data in PostgreSQL, imports provider snapshots from Azure Blob Storage, synchronizes Yahoo data, and exposes the stored data through both an HTTP API and an MCP endpoint.

The service is Postgres-only. Provide `DBConnectionString` for the database and `AZURE_STORAGE_CONNECTION_STRING` for provider snapshots.

## What it does

- Imports Sleeper, SportsData, and FantasyPros snapshots from Azure Blob Storage
- Persists and enriches player records in Postgres
- Records provider snapshot import attempts in dedicated sync-run tables
- Supports Yahoo OAuth bootstrap and token refresh for authenticated Fantasy Sports API access (tokens are stored in the `yahoo_oauth_state` table)
- Supports scheduled and manual Yahoo weekly stat syncs for scoring and reads
- Exposes league, roster, waiver, player, schedule, and scoring operations over HTTP and MCP

## How it works

The app has three main layers:

1. **Import and sync layer**
   - Repository data-sync workflows collect Sleeper, SportsData, and FantasyPros data and upload snapshots to Azure Blob Storage.
   - Snapshot import endpoints load the referenced blobs and persist normalized data.
   - `NightlyYahooSyncService` optionally synchronizes Yahoo weekly stats on startup and at the configured UTC hour.

2. **Persistence layer**
   - `PlayerCatalogStore` persists players and Sleeper sync-run metadata.
   - Provider import services persist their own sync-run metadata.
   - `YahooAuthStateStore` persists Yahoo OAuth tokens.

3. **API and tool layer**
   - The HTTP API exposes operations backed by persisted data.
   - MCP tools expose agent-facing league operations at `/mcp`.

## Configuration

Configuration lives in `appsettings.json`.

### Environment variables

The service respects these environment variables (prefer these over editing config files):

**Database:**
- `DBConnectionString` - **Required.** PostgreSQL connection string. The service fails fast on startup if this is missing or empty.

**Snapshot storage:**
- `AZURE_STORAGE_CONNECTION_STRING` - **Required.** Azure Storage connection string for provider snapshot blobs.

**ASP.NET Core:**
- `ASPNETCORE_URLS` - listening URL; e.g., `http://127.0.0.1:5181`
- `ASPNETCORE_ENVIRONMENT` - `Development` or `Production`

**Example startup with env vars:**

```bash
export DBConnectionString="postgresql://user:pass@localhost/leaguedb"
export AZURE_STORAGE_CONNECTION_STRING="your-storage-connection-string"
export ASPNETCORE_URLS="http://127.0.0.1:5181"
dotnet run
```

### Snapshot imports

Sleeper, SportsData, and FantasyPros imports are triggered through their HTTP endpoints with the Azure Blob container and blob names produced by the corresponding data-sync workflow. Configure `AZURE_STORAGE_CONNECTION_STRING` so the API can read those snapshots.

### Database connection

```json
"DBConnectionString": ""
```

Required. The service fails fast on startup if this is missing. Prefer setting it via the `DBConnectionString` environment variable so it can point at any Postgres host (local docker, managed Postgres, etc.).

### Yahoo data

YahooDataSync owns Yahoo OAuth and provider synchronization. LeagueAPI imports the produced snapshots and exposes the persisted Yahoo data through its HTTP and MCP operations.

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

### Required runtime configuration

- `DBConnectionString` - PostgreSQL connection string. The service fails fast without it.
- `AZURE_STORAGE_CONNECTION_STRING` - Azure Storage connection string for provider snapshots. The service fails fast without it.

### Optional runtime configuration

- `ASPNETCORE_URLS` - ASP.NET Core listening URL. Example: `http://127.0.0.1:5181`

### Example startup command

```bash
export DBConnectionString="Host=localhost;Database=leaguedb;Username=postgres;Password=your-password"
export AZURE_STORAGE_CONNECTION_STRING="your-storage-connection-string"
export ASPNETCORE_URLS="http://127.0.0.1:5181"

dotnet run
```

## Startup behavior

When the service starts:

1. It validates `DBConnectionString` and `AZURE_STORAGE_CONNECTION_STRING` and fails fast if either is missing.
2. It serves its HTTP API and MCP endpoint.

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

### Import a Sleeper snapshot

`POST /api/sync/sleeper`

The request identifies a snapshot already uploaded to Azure Blob Storage:

```json
{
 "containerName": "provider-snapshots",
 "blobName": "sleeper/players.json",
 "retrievedAtUtc": "2026-07-16T12:00:00Z"
}
```

### Get latest Yahoo sync status

`GET /api/sync/yahoo/latest?gameKey=&season=&week=`

Returns the latest Yahoo sync run overall or for a specific game/season/week filter.

Example:

```bash
curl "http://127.0.0.1:5181/api/sync/yahoo/latest?gameKey=449&season=2024&week=1"
```

### Import a Yahoo weekly snapshot

`POST /api/sync/yahoo`

The JSON request body supplies `containerName`, `blobName`, `gameKey`, `season`, `week`, and `retrievedAtUtc`. YahooDataSync creates the snapshot and calls this endpoint after uploading it to Azure Blob Storage.

Example:

```bash
curl -X POST http://127.0.0.1:5181/api/sync/yahoo \
   -H "Content-Type: application/json" \
   -d '{"containerName":"yahoodata","blobName":"snapshots/2024/week-1.json","gameKey":"449","season":2024,"week":1,"retrievedAtUtc":"2024-09-10T00:00:00Z"}'
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

## Yahoo end-to-end validation

To validate imported Yahoo data, you need all of these in place:

1. a real PostgreSQL connection string in `DBConnectionString`
2. the EF migrations applied
3. a Yahoo snapshot uploaded by YahooDataSync
4. at least one active scoring template in the database

### 1. Configure the database connection

Set `DBConnectionString` to a real Postgres database before importing or reading Yahoo data.

### 2. Apply the database migrations

From `src/LeagueAPI`:

```bash
dotnet ef database update
```

### 3. Import a Yahoo snapshot

Use YahooDataSync to retrieve and upload the provider snapshot, then call `POST /api/sync/yahoo` with its Azure Blob Storage location.

### 4. Seed an active scoring template

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

### 5. Validate import status

```bash
curl "http://127.0.0.1:5181/api/sync/yahoo/latest?gameKey=449&season=2024&week=1"
```

You want to see:

- `Status = "Succeeded"`
- `RecordCount > 0`
- `MatchedPlayerCount > 0`

### 6. Validate raw stat reads

```bash
curl "http://127.0.0.1:5181/api/yahoo/stats/2024/1?position=QB&limit=5"
curl "http://127.0.0.1:5181/api/yahoo/stats/player/4046/2024/week/1"
```

You want to see normalized stat rows like stat `4`, `5`, `6`, `9`, `10`, `11`, `12`, and `13` when Yahoo reported them for that player/week.

### 7. Validate scored point reads

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
- Snapshot imports avoid reprocessing the same provider blob and retrieval timestamp.
- The full raw Sleeper payload for each player is preserved on the `players.RawJson` column so later ingestion changes can be made without losing the original data.
- The model already includes `yahoo_id`, so cross-provider lookups are supported when Sleeper includes the mapping.
- Yahoo OAuth tokens are stored in the `yahoo_oauth_state` Postgres table.
- Yahoo weekly sync stores one raw player payload per player/week plus normalized stat rows and recalculated points for every active scoring template.
- Yahoo read endpoints intentionally return normalized stat and point data, not the stored `RawJson` audit payload.

## Current status

The service is Postgres-backed. To use it:

1. Have a Postgres database reachable (locally, the repo's `docker-compose.yml` starts one).
2. Set `DBConnectionString` to point at it.
3. Set `AZURE_STORAGE_CONNECTION_STRING` to the storage account containing provider snapshots.
4. Run `dotnet ef database update` from `src/LeagueAPI` to apply migrations.
5. Start the service, import provider snapshots, and query the HTTP API or MCP tools.
