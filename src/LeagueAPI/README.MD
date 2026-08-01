# LeagueAPI

`LeagueAPI` stores fantasy football data in PostgreSQL, imports provider snapshots from Azure Blob Storage, and exposes the stored data through both an HTTP API and an MCP endpoint.

The service is Postgres-only. Provide `DBConnectionString` for the database and `AZURE_STORAGE_CONNECTION_STRING` for provider snapshots.

## What it does

- Imports Sleeper, SportsData, and FantasyPros snapshots from Azure Blob Storage
- Persists and enriches player records in Postgres
- Records provider snapshot import attempts in dedicated sync-run tables
- Imports FantasyPros weekly fantasy points into `weekly_player_scores` and exposes them as the league's points source
- Exposes league, roster, waiver, player, schedule, and scoring operations over HTTP and MCP

## How it works

The app has three main layers:

1. **Import and sync layer**
   - Repository data-sync workflows collect Sleeper, SportsData, and FantasyPros data and upload snapshots to Azure Blob Storage.
   - Snapshot import endpoints load the referenced blobs and persist normalized data.

2. **Persistence layer**
   - `PlayerCatalogStore` persists players and Sleeper sync-run metadata.
   - Provider import services persist their own sync-run metadata.

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

### Get latest FantasyPros player snapshot sync status

`GET /api/sync/fantasypros/latest`

### Import a FantasyPros player snapshot

`POST /api/sync/fantasypros`

The JSON request body supplies `containerName`, `blobName`, `season`, `week`, and `retrievedAtUtc`.

### Get latest FantasyPros points sync status

`GET /api/sync/fantasypros/points/latest?season=`

Returns the latest FantasyPros points sync run, optionally filtered to a season.

Example:

```bash
curl "http://127.0.0.1:5181/api/sync/fantasypros/points/latest?season=2025"
```

### Import a FantasyPros weekly points snapshot

`POST /api/sync/fantasypros/points`

The JSON request body supplies `containerName`, `blobName`, `requestedSeason`, `servedSeason`, `servedScoring`, `endWeek`, and `retrievedAtUtc`. Rows are persisted to `weekly_player_scores`.

Example:

```bash
curl -X POST http://127.0.0.1:5181/api/sync/fantasypros/points \
   -H "Content-Type: application/json" \
   -d '{"containerName":"fantasypros","blobName":"points/2025/weekly.json","requestedSeason":2025,"servedSeason":"2025","servedScoring":"HALF","endWeek":18,"retrievedAtUtc":"2025-09-10T00:00:00Z"}'
```

### Get weekly fantasy points leaderboard

`GET /api/points/{season}/{week}?position=&limit=`

Returns FantasyPros weekly fantasy points for that week, highest first. `limit` defaults to 25.

Example:

```bash
curl "http://127.0.0.1:5181/api/points/2025/1?position=QB&limit=10"
```

### Get weekly fantasy points for one player

`GET /api/points/player/{sleeperPlayerId}/{season}/week/{week}`

Responds with `404 Not Found` when no score row exists for that player/week.

Example:

```bash
curl "http://127.0.0.1:5181/api/points/player/4046/2025/week/1"
```

### Get season fantasy point totals for one player

`GET /api/points/player/{sleeperPlayerId}/{season}`

Returns the season total plus a week-by-week breakdown.

Example:

```bash
curl "http://127.0.0.1:5181/api/points/player/4046/2025"
```

## Points end-to-end validation

To validate imported points data, you need all of these in place:

1. a real PostgreSQL connection string in `DBConnectionString`
2. the EF migrations applied
3. a FantasyPros points snapshot uploaded to Azure Blob Storage

### 1. Configure the database connection

Set `DBConnectionString` to a real Postgres database before importing or reading points data.

### 2. Apply the database migrations

From `src/LeagueAPI`:

```bash
dotnet ef database update
```

### 3. Import a FantasyPros points snapshot

Call `POST /api/sync/fantasypros/points` with the snapshot's Azure Blob Storage location.

### 4. Validate import status

```bash
curl "http://127.0.0.1:5181/api/sync/fantasypros/points/latest?season=2025"
```

You want to see:

- `Status = "Succeeded"`
- `RecordCount > 0`
- `MatchedPlayerCount > 0`

### 5. Validate scored point reads

```bash
curl "http://127.0.0.1:5181/api/points/2025/1?position=QB&limit=5"
curl "http://127.0.0.1:5181/api/points/player/4046/2025/week/1"
curl "http://127.0.0.1:5181/api/points/player/4046/2025"
```

You want to see:

- scored rows returned for the requested week
- season totals adding up from weekly rows

## MCP endpoint

The project also exposes an HTTP MCP transport at:

- `POST /mcp`

Current tools:

- `GetPlayerBySleeperId`
- `SearchPlayers`
- `GetAvailablePlayers`
- `GetPlayerAvailability`
- `GetLatestSleeperSyncStatus`
- `GetLatestSportsDataSyncStatus`

Roster, waiver, agent-profile, and league-state tools are also registered.

This route is meant for MCP clients, not normal browser navigation. A plain `GET /mcp` is expected to return `405 Method Not Allowed`.

## Important implementation notes

- Player reads are based on **stored data** in Postgres, not live calls to Sleeper.
- Snapshot imports avoid reprocessing the same provider blob and retrieval timestamp.
- The full raw Sleeper payload for each player is preserved on the `players.RawJson` column so later ingestion changes can be made without losing the original data.
- The player model keeps `yahoo_id` because it is the first tier of the FantasyPros-to-Sleeper player id bridge.
- Weekly fantasy points come from FantasyPros and are stored in the `weekly_player_scores` table.

## Current status

The service is Postgres-backed. To use it:

1. Have a Postgres database reachable (locally, the repo's `docker-compose.yml` starts one).
2. Set `DBConnectionString` to point at it.
3. Set `AZURE_STORAGE_CONNECTION_STRING` to the storage account containing provider snapshots.
4. Run `dotnet ef database update` from `src/LeagueAPI` to apply migrations.
5. Start the service, import provider snapshots, and query the HTTP API or MCP tools.
