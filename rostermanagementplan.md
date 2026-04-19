# Roster Management and Player Research Tool Plan

## Current State

- `LeagueAPI` already exposes roster and player data through HTTP and MCP.
- `FantasyAgent` currently only has local tools for profile/bootstrap/logo work.
- The next agent step is to let agents **use** the LeagueAPI roster and research capabilities during decision-making.

## Recommended Approach

Build the agent tool set in two layers:

1. **Core LeagueAPI-backed tools** the agent can call directly for roster actions and player lookup
2. **Optional composite tools** that combine multiple calls into one higher-level fantasy workflow

For the first pass, focus on the **core tools**. They map closely to the current LeagueAPI MCP surface and will give agents everything needed to inspect their roster, find available players, research players, and claim/drop them.

## Phase 1: Core Agent Tools

### Roster management tools

| Tool | Description | Backing source | Priority |
| --- | --- | --- | --- |
| `GetMyRoster` | Returns the current roster for the agent, including ownership and acquisition metadata. | Existing `LeagueAPI` MCP `RosterTools.GetMyRoster` | High |
| `GetAvailablePlayers` | Returns active players that are not currently on any roster, with filters for team, position, bye week, projected points, ADP, sorting, and limit. | Existing `LeagueAPI` MCP `RosterTools.GetAvailablePlayers` | High |
| `GetPlayerAvailability` | Returns whether a specific player is available and, if not, which agent owns them. | Existing `LeagueAPI` MCP `RosterTools.GetPlayerAvailability` | High |
| `SearchPlayersWithOwnership` | Searches the full player pool while also showing availability and owner information. Useful when the agent wants broad roster context, not just free agents. | Existing `LeagueAPI` MCP `RosterTools.SearchPlayersWithOwnership` | High |
| `AddPlayerToRoster` | Claims a player for the agent. Should fail cleanly when the player is already owned by someone else. | Existing `LeagueAPI` MCP `RosterTools.AddPlayerToRoster` | High |
| `RemovePlayerFromRoster` | Drops a player from the agent's roster. | Existing `LeagueAPI` MCP `RosterTools.RemovePlayerFromRoster` | High |

### Player research tools

| Tool | Description | Backing source | Priority |
| --- | --- | --- | --- |
| `GetPlayerBySleeperId` | Returns a single player record by Sleeper ID. Useful once the agent has already identified a player. | Existing `LeagueAPI` MCP `PlayerCatalogTools.GetPlayerBySleeperId` | High |
| `GetPlayerByYahooId` | Returns a single player record by Yahoo ID. Useful when working from Yahoo stat data. | Existing `LeagueAPI` MCP `PlayerCatalogTools.GetPlayerByYahooId` | Medium |
| `SearchPlayers` | Searches players by name, team, position, bye week, ADP, and projected points. This is the main discovery tool for player research. | Existing `LeagueAPI` MCP `PlayerCatalogTools.SearchPlayers` | High |
| `GetPlayerWeeklyStats` | Returns a player's raw weekly Yahoo stat line. Useful for understanding how the points were earned. | Existing `LeagueAPI` MCP `YahooReadTools.GetPlayerWeeklyStats` | Medium |
| `GetPlayerWeeklyPoints` | Returns a player's fantasy points for a given week. | Existing `LeagueAPI` MCP `YahooReadTools.GetPlayerWeeklyPoints` | High |
| `GetPlayerSeasonPoints` | Returns season totals and weekly point history for a player. | Existing `LeagueAPI` MCP `YahooReadTools.GetPlayerSeasonPoints` | High |
| `GetTopScorersByWeek` | Returns weekly leaders, optionally filtered by position. Useful for trend spotting and identifying hot players. | Existing `LeagueAPI` MCP `YahooReadTools.GetTopScorersByWeek` | Medium |
| `GetScoringTemplates` | Returns the scoring rules being used so the agent can reason correctly about value. | Existing `LeagueAPI` MCP `YahooReadTools.GetScoringTemplates` | Medium |

## Phase 2: Composite Agent Tools

These are not required for the first integration, but they would make agent decision-making much easier by reducing multi-step reasoning.

| Tool | Description | Depends on |
| --- | --- | --- |
| `ResearchAvailablePlayersByPosition` | Returns available players for a position plus enough summary context for quick comparison. | `GetAvailablePlayers`, `GetPlayerSeasonPoints`, `GetPlayerWeeklyPoints` |
| `ComparePlayers` | Takes 2-5 candidate players and returns a side-by-side research summary. | `GetPlayerBySleeperId`, `GetPlayerSeasonPoints`, `GetPlayerWeeklyPoints` |
| `FindBestAvailableForNeed` | Lets the agent ask for players that fit a roster need such as upside, floor, bye week coverage, or position depth. | `GetAvailablePlayers`, `SearchPlayersWithOwnership`, scoring tools |
| `EvaluateRosterGap` | Reviews the current roster and identifies weak spots, depth issues, and replacement targets. | `GetMyRoster`, player research tools |
| `PreviewAddDropMove` | Shows the before/after effect of a proposed add/drop move before the agent commits it. | `GetMyRoster`, `GetAvailablePlayers`, `GetPlayerSeasonPoints` |

## Agent Integration Work Needed

The following plumbing work still needs to happen in `AgenticLeague`:

1. Add LeagueAPI MCP client access to `FantasyAgent`
2. Register the selected LeagueAPI-backed tools with the agent
3. Update the agent prompt so it knows:
   - check its roster before making moves
   - research available players before adding one
   - verify availability before attempting a claim
   - record important conclusions in bootstrap or later memory/logging tools

## Recommended Build Order

1. Wire `FantasyAgent` to the LeagueAPI MCP server
2. Expose the **Phase 1 roster management tools**
3. Expose the **Phase 1 player research tools**
4. Update the fantasy agent prompt to explain when to use each tool
5. Add the **Phase 2 composite tools** only after the direct tools are working well

## Suggested First Cut

If you want the smallest useful set first, start with these six:

1. `GetMyRoster`
2. `GetAvailablePlayers`
3. `GetPlayerAvailability`
4. `SearchPlayers`
5. `AddPlayerToRoster`
6. `RemovePlayerFromRoster`

That is enough for an agent to understand its team, find candidates, verify ownership, and make roster changes.
