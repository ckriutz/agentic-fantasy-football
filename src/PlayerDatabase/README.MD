# PlayerDatabase

`PlayerDatabase` is the Step 1 service for the fantasy football project. It pulls the NFL player catalog from the Sleeper API, stores a daily raw snapshot locally, tracks sync state, and exposes the stored data through both a normal HTTP API and an MCP endpoint.

Right now, the project is designed to work well **without committing to a final database choice yet**. If no database connection string is configured, it runs in snapshot-backed mode and serves data from the latest saved Sleeper snapshot.

## What it does

- Downloads the Sleeper NFL players payload from:
  - `https://api.sleeper.app/v1/players/nfl`
- Saves one raw JSON snapshot per day under `data/sleeper/`
- Tracks the latest sync status in `data/sleeper/latest-sync.json`
- Avoids re-pulling Sleeper more than once per day unless you force a sync
- Exposes player lookup and search endpoints over HTTP
- Exposes the same player lookup capabilities as MCP tools on `/mcp`

## How it works

The app has three main layers:

1. **Sync layer**
   - `NightlySleeperSyncService` runs as a hosted background service.
   - It can sync once on startup and then again every day at the configured UTC hour.
   - It calls Sleeper through `SleeperApiClient`.

2. **Persistence layer**
   - `FileSleeperSnapshotStore` writes the raw daily JSON snapshot.
   - `JsonFileSyncStateStore` stores the latest sync metadata.
   - A SQL-backed persistence path exists in the codebase, but it is optional and only activates when a connection string is configured.

3. **Read layer**
   - The HTTP API exposes player queries.
   - MCP tools expose the same read functionality at `/mcp`.
   - In snapshot mode, reads are served from the latest saved Sleeper snapshot.

## Storage modes

The app supports these storage modes through `PlayerCatalog:Mode`:

- `Auto` - default; uses SQL if `ConnectionStrings:PlayerDatabase` is present, otherwise uses snapshot mode
- `SnapshotOnly` - always read from the saved Sleeper snapshot
- `SqlServer` - force SQL-backed persistence and reads

Current default:

```json
"PlayerCatalog": {
  "Mode": "Auto"
}
```

With the current config, **no connection string means snapshot mode**.

## Files created by the service

By default, the app writes:

- `data/sleeper/players-YYYY-MM-DD.json` - the latest raw Sleeper payload for that day
- `data/sleeper/latest-sync.json` - metadata about the most recent sync

The snapshot directory is ignored in git.

## Configuration

Configuration lives in `appsettings.json`.

### Sleeper sync settings

```json
"SleeperSync": {
  "Enabled": true,
  "RunOnStartup": true,
  "DailySyncHourUtc": 6,
  "PlayersEndpoint": "https://api.sleeper.app/v1/players/nfl",
  "SnapshotDirectory": "data/sleeper",
  "StateFilePath": "data/sleeper/latest-sync.json"
}
```

### Optional database connection

```json
"ConnectionStrings": {
  "PlayerDatabase": ""
}
```

Leave this empty for now if you want to stay snapshot-only while deciding on the long-term database.

## Running the project

From the `src/PlayerDatabase` directory:

```bash
dotnet build
dotnet run
```

If you want to pin the listening URL:

```bash
ASPNETCORE_URLS=http://127.0.0.1:5181 dotnet run
```

## Startup behavior

When the service starts:

1. It reads the configured storage mode.
2. If `SleeperSync:Enabled` is `true`, the hosted sync service starts.
3. If `SleeperSync:RunOnStartup` is `true`, it attempts an immediate sync.
4. If a successful sync already happened today, the sync is skipped.
5. The service then waits until the next configured UTC sync hour and repeats the process.

## HTTP API

### Service info

`GET /`

Returns a small summary of the service, the configured/effective storage mode, and the key routes.

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

## MCP endpoint

The project also exposes an HTTP MCP transport at:

- `POST /mcp`

Current tools:

- `GetPlayerBySleeperId`
- `GetPlayerByYahooId`
- `SearchPlayers`
- `GetLatestSleeperSyncStatus`

Player lookup and search MCP tools only return active players.

This route is meant for MCP clients, not normal browser navigation. A plain `GET /mcp` is expected to return `405 Method Not Allowed`.

## Important implementation notes

- Player reads are based on **stored data**, not live calls to Sleeper.
- The once-per-day guard is based on the last successful sync date in UTC.
- The raw Sleeper payload is preserved so later ingestion changes can be made without losing the original snapshot.
- The model already includes `yahoo_id`, so cross-provider lookups are supported when Sleeper includes the mapping.

## Current status

Today, the most stable way to use this project is:

1. Run it in snapshot-backed mode.
2. Let it sync and save the daily Sleeper player snapshot.
3. Use the HTTP API or MCP tools to query the stored player catalog.

The SQL-backed path is present in the code, but the project is intentionally not tied to a final database choice yet.
