# SportsDataIO Integration Plan (LeagueAPI)

## Objective
Enrich Sleeper-backed player data with SportsDataIO fantasy metrics so agents can make stronger draft/start decisions, while preserving current LeagueAPI behavior and exposing the new data through both API and MCP.

## Confirmed scope
- Source endpoint for this increment: `FantasyPlayers` from SportsDataIO.
- Key fields to ingest and expose: **AverageDraftPosition**, **ByeWeek**, **LastSeasonFantasyPoints**, **ProjectedFantasyPoints**, **AuctionValue**.
- Expose data in two ways:
  - First-class nullable fields on existing player responses (PlayerRecord/PlayerEntity).
  - Enhanced search/filter/sort capabilities on existing API and MCP surfaces using these fields.
- True "last week fantasy points" is **out of scope** here and will come from Yahoo integration.

## Current system baseline
- Sleeper sync already exists (`SleeperPlayerSyncService`, `NightlySleeperSyncService`).
- Players are persisted in Postgres (`players`) with sync-run tracking (`sleeper_sync_runs`).
- Sleeper model includes `FantasyDataId`, which aligns with SportsDataIO `PlayerID`.
- Existing surfaces:
  - REST: `/api/players/*`, `/api/sync/sleeper/*`
  - MCP tools: get/search players and Sleeper sync status

## Architecture note: PlayerEntity vs PlayerRecord
- **`PlayerEntity`** is the EF Core database row — it maps directly to the `players` table.
- **`PlayerRecord`** is the API/MCP-facing DTO — it's what gets serialized and returned to callers.
- Both exist for separation of concerns (DB schema vs public contract). Both need the five new SportsData fields added as nullable properties.

## Key linkage decision
- **Join key:** `players.FantasyDataId == sportsdata_fantasy_players.SportsDataPlayerId` (query-time join, no hard FK constraint).
- SportsData records live in their own table; enrichment fields are copied onto `PlayerEntity`/`PlayerRecord` during sync for fast access.
- No fallback name-matching needed for this increment — if `FantasyDataId` is missing on a Sleeper player, the SportsData fields simply remain null.

## Detailed implementation steps

### Phase 1: Configuration and security ✅ DONE
1. Add a `SportsDataSyncOptions` configuration model:
   - `Enabled`
   - `BaseUrl`
   - `FantasyPlayersEndpoint`
   - `DailySyncHourUtc`
   - `RunOnStartup`
   - `ApiKey` (from environment variable / secret provider — never committed)
2. Add config section in `appsettings.json` with non-secret defaults.
3. Register options in `Program.cs`.

### Phase 2: Domain and persistence models ✅ DONE
1. Add SportsData DTO model (`SportsDataFantasyPlayer`) for the `FantasyPlayers` JSON payload with all fields from the API response.
2. Add EF entity `SportsDataFantasyPlayerEntity` for its own table (`sportsdata_fantasy_players`):
   - `SportsDataPlayerId` (PK, int — maps to API `PlayerID`)
   - `Name`, `Team`, `Position`
   - `AverageDraftPosition` (decimal?)
   - `AverageDraftPositionPPR` (decimal?)
   - `ByeWeek` (int?)
   - `LastSeasonFantasyPoints` (decimal?)
   - `ProjectedFantasyPoints` (decimal?)
   - `AuctionValue` (int?)
   - `AuctionValuePPR` (int?)
   - `FantasyPlayerKey` (string?)
   - `RawJson` (text)
   - `UpdatedAtUtc` (timestamptz)
3. Add sync-run entity `SportsDataSyncRun` for table `sportsdata_sync_runs`:
   - `SyncRunId` (PK, Guid)
   - `StartedAtUtc`, `CompletedAtUtc`, `Status`, `RecordCount`, `ErrorMessage`
4. Add five nullable fields to **`PlayerEntity`** (the `players` table):
   - `AverageDraftPosition` (decimal?)
   - `ByeWeek` (int?)
   - `LastSeasonFantasyPoints` (decimal?)
   - `ProjectedFantasyPoints` (decimal?)
   - `AuctionValue` (int?)
5. Add matching five nullable fields to **`PlayerRecord`** (the API DTO).
6. Update `LeagueApiDbContext` with new entity mappings and indexes:
   - Index on `sportsdata_fantasy_players.SportsDataPlayerId` (PK)
   - Index on `players.ByeWeek` (for bye-week filtering)
7. Create EF migration.

### Phase 3: SportsData ingestion ✅ DONE
1. Implement `SportsDataApiClient` using `HttpClient`:
   - Fetches `FantasyPlayers` endpoint with API key as query param.
   - Returns raw JSON string (consistent with `SleeperApiClient` pattern).
2. Implement `SportsDataPlayerSyncService`:
   - Fetch and deserialize the payload.
   - Upsert all rows into `sportsdata_fantasy_players` table.
   - Cross-reference with `players` table by `FantasyDataId`:
     - For each Sleeper player where `FantasyDataId` matches a SportsData `PlayerID`, copy the five enrichment fields onto the `players` row.
     - Players without a matching `FantasyDataId` simply keep null values.
   - Record sync run (started/completed/failed) in `sportsdata_sync_runs`.
   - Log counts: total fetched, linked to Sleeper, unlinked.
3. Reuse Sleeper sync service patterns for consistency.

### Phase 4: Scheduling and startup wiring ✅ DONE
1. Add `NightlySportsDataSyncService` background service.
2. Respect `Enabled`, `RunOnStartup`, and `DailySyncHourUtc` from options.
3. Register all new services and dependencies in `Program.cs`.

### Phase 5: API and MCP exposure ✅ DONE

#### Enriched existing player responses
- `PlayerRecord` gains five new fields. All existing endpoints and MCP tools that return players automatically include them (nullable, backward-compatible).

#### Enhanced search/filter/sort on existing surfaces
Extend `PlayerQuery` model and both `GET /api/players` and MCP `SearchPlayers` tool with:
- **New filter parameters:**
  - `byeWeek` (int?) — filter players by bye week
  - `minProjectedPoints` (decimal?) — minimum projected fantasy points
  - `maxAverageDraftPosition` (decimal?) — only players drafted at or before this ADP
- **New sort parameter:**
  - `sortBy` (string?) — options: `projectedPoints`, `adp`, `lastSeasonPoints`, `auctionValue`, `name` (default)
  - `sortDescending` (bool?) — default false

#### New sync status endpoint and MCP tool
- `GET /api/sync/sportsdata/latest` — returns latest SportsData sync run state.
- MCP tool `GetLatestSportsDataSyncStatus` — same data for agents.

#### No separate SportsData player endpoints needed
Since the enrichment fields live directly on the player responses, and search/filter supports SportsData fields, a parallel `/api/sportsdata/players/*` surface is unnecessary overhead. Agents and front-end query one unified player surface.

### Phase 6: Validation and readiness ✅ DONE
1. Run `dotnet build` to verify compilation.
2. Verify:
   - Sync executes, stores rows in `sportsdata_fantasy_players`, and copies enrichment to `players`.
   - Existing player queries return new fields (null when no SportsData match).
   - New filter/sort parameters work on API and MCP.
   - Sync status endpoint reports accurate metadata.

## Proposed file-level change map
- `src/LeagueAPI/Configuration/`:
  - add `SportsDataSyncOptions.cs`
- `src/LeagueAPI/Models/`:
  - add `SportsDataFantasyPlayer.cs` (API DTO)
  - add `SportsDataFantasyPlayerEntity.cs` (EF entity)
  - add `SportsDataSyncRun.cs` (EF entity)
  - update `PlayerEntity.cs` (add 5 enrichment fields)
  - update `PlayerRecord.cs` (add 5 enrichment fields)
  - update `PlayerQuery.cs` (add filter/sort params)
- `src/LeagueAPI/Data/`:
  - update `LeagueApiDbContext.cs` (new entity mappings + indexes)
- `src/LeagueAPI/Services/`:
  - add `SportsDataApiClient.cs`
  - add `SportsDataPlayerSyncService.cs`
  - update `PostgresPlayerCatalogStore.cs` (apply filters/sorts in queries)
  - update `SnapshotPlayerCatalogReader.cs` (apply filters/sorts in queries)
  - update `PlayerRecordFactory.cs` (map enrichment fields)
- `src/LeagueAPI/HostedServices/`:
  - add `NightlySportsDataSyncService.cs`
- `src/LeagueAPI/Tools/`:
  - update `PlayerCatalogTools.cs` (add filter/sort params to SearchPlayers, add sync status tool)
- `src/LeagueAPI/Program.cs`:
  - service registrations + sync status route
- `src/LeagueAPI/Migrations/`:
  - add migration for new tables + player column additions
- `src/LeagueAPI/appsettings.json`:
  - add SportsData config section (no secrets)

## Risks and mitigations
- **Missing `FantasyDataId` on some Sleeper records:** enrichment fields stay null — no data corruption. Agents see partial enrichment, which is acceptable.
- **Payload shape drift from SportsDataIO:** robust deserialization with nullable fields; sync errors are logged and surfaced via status endpoint.
- **Scope creep into weekly points:** explicitly out of scope; tracked in Yahoo integration backlog.

## Deliverable definition for this increment
- SportsDataIO `FantasyPlayers` sync runs on schedule and persists in its own DB table.
- Sleeper players with matching `FantasyDataId` get five enrichment fields populated.
- All existing player API/MCP responses include the new fields automatically.
- Agents can filter/sort players by bye week, projected points, ADP, and auction value.
- Sync status is visible via dedicated endpoint and MCP tool.
