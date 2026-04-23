# Fantasy Football Agent Guide

## Purpose

This guide defines how an AI agent should use available tools and interpret player data to make fantasy football decisions. Use it as a reference for roster management, player evaluation, and lineup setting.

---

## Available Tools

### `SearchPlayers`
**Use when:** Looking up a specific player by name, team, position, or bye week.

Returns: player stats, ownership percentage, and availability status.

### `GetAvailablePlayers`
**Use when:** Finding free agents to add to a roster.

Returns: all unowned players with stats and availability metadata.

### `GetMyRoster`
**Use when:** Viewing your current roster, including starters and bench players.

Returns: all players currently on your roster with stats and availability metadata.

### `SearchWeb`
**Use when**: You need to look up players, teams, positions, and fantasy football guidance. Very helpful for researching players.

Returns: Research data.

---

## Player Data Reference

| Field | Meaning | How to use |
|---|---|---|
| `search_rank` | Overall player ranking; lower = better. `9999999` = unranked/insufficient data | Primary sort key when comparing players |
| `auctionValue` | Estimated auction draft value; higher = more desirable | Use to gauge relative value in trade/add decisions |
| `projectedFantasyPoints` | Expected points this season | Prefer players with higher projections |
| `lastSeasonFantasyPoints` | Actual points scored last season | Use to validate projections; flag large discrepancies |
| `averageDraftPosition` | Average pick position in drafts; lower = higher demand | Use to assess market consensus on a player |
| `byeWeek` | Week the player's team has no game | **Never start a player on their bye week** |

---

## Roster Management Rules

### Starting Lineup
- Only players in the **starting lineup** earn points each week.
- Players on the **bench** earn 0 points regardless of their performance.
- A player on **bye** earns 0 points — remove them from the starting lineup for that week.
- A player in the starting lineup who **does not play** (injury, coach's decision) earns 0 points.

### Decision Logic

**When setting a lineup:**
1. Check each starter's `byeWeek` — bench any player on bye.
2. Check injury/availability status — bench any player who is out.
3. Compare `projectedFantasyPoints` among eligible players at each position — start the highest projection.

**When a roster spot is weak:**
1. Call `GetAvailablePlayers` filtered by the needed position.
2. Sort results by `search_rank` (ascending) or `projectedFantasyPoints` (descending).
3. If the free agent outperforms the current roster player, recommend the add/drop.

**When evaluating a trade:**
1. Use `SearchPlayers` to pull stats on all players involved.
2. Compare `projectedFantasyPoints`, `lastSeasonFantasyPoints`, and `auctionValue` on both sides.
3. Factor in `byeWeek` conflicts for the current roster.

### Goal
Maximize total points scored each week by fielding the best available starting lineup.

## Roster Management Tools
When you need to add/drop players or set your lineup, use the following tools:
- `AddPlayerToRoster`: Add a player to your roster from free agency.
- `RemovePlayerFromRoster`: Remove a player from your roster.
- `GetMyRoster`: View your current roster, including starters and bench players.

When conducting player research, use the following tools:
- `SearchWeb`: Research current player news, injuries, depth chart changes, matchup context, and rankings. When using this tool, it may help to search for players that you're considering adding to your roster, as well as players currently on your roster to stay up to date on their status and outlook for the season. Also searching for players in certain positions can help you identify players.
- `GetPlayerBySleeperId`: Look up a specific player by Sleeper player ID to get their stats, ownership percentage, and availability status.

Any research notes and thoughts can be added to your bootstrap file to keep track of your evolving strategy and team information.
