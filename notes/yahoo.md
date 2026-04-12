## Details about the Yahoo Keys and OAuth data.

## NOTES

### What was verified

- OAuth flow works and authenticated requests to the Yahoo Fantasy Sports API succeed.
- Weekly player stats are available after the week is complete.
- There are two useful scopes for weekly data:
	- `game` scope: raw player stats for the entire player pool
	- `league` scope: raw player stats plus fantasy points based on that league's scoring rules

### Do I need a Yahoo league?

No, not if the goal is to get raw football results.

Use `game/{game_key}` endpoints when the goal is to retrieve player outcomes such as:

- passing yards
- passing touchdowns
- interceptions
- rushing yards
- rushing touchdowns
- receptions
- receiving yards
- receiving touchdowns

Example:

`/fantasy/v2/game/{game_key}/players;start=0;count=25/stats;type=week;week=1`

This returns week-specific player stat lines without requiring a personal Yahoo league.

Yes, if the goal is to get Yahoo fantasy points as scored under a specific ruleset.

Use `league/{league_key}` endpoints when the goal is to retrieve:

- fantasy points
- roster context
- matchup results
- standings
- waivers
- any decision that depends on league scoring settings

Example:

`/fantasy/v2/league/{league_key}/players;start=0;count=25/stats;type=week;week=1`

This returns week-specific player stat lines and `player_points` for that league.

### Important distinction

- `game` scope gives raw stats and is enough if the agent will calculate fantasy points itself.
- `league` scope gives Yahoo-scored fantasy points, which are league-specific.

Because fantasy points depend on scoring settings, there is no single universal Yahoo fantasy-point number outside league context.

### Recommended approach for agents

If the goal is to evaluate players generally, the cleanest setup is:

1. Pull raw weekly stats from `game/{game_key}`.
2. Maintain a scoring template in code.
3. Compute fantasy points internally.

This avoids needing to create a Yahoo league just to test agent behavior.

If the goal is to manage a real Yahoo team, use a real `league_key` and fetch:

1. league settings
2. roster
3. weekly player stats
4. weekly player points
5. standings and transactions

### Live-tested examples

Completed 2024 NFL game key found via API:

- `461` = 2025 NFL season
- `449` = 2024 NFL season

Verified working endpoints:

- `/fantasy/v2/games;game_codes=nfl;seasons=2025`
- `/fantasy/v2/game/449/players;start=0;count=3/stats;type=week;week=1`
- `/fantasy/v2/league/449.l.246662/players;start=0;count=3/stats;type=week;week=1`
- `/fantasy/v2/league/449.l.246662/players;start=0;count=3;sort=PTS;sort_type=week;sort_week=1/stats;type=week;week=1`

Verified behavior from live calls:

- `game/449/...` returned week 1 raw stats for players but no `player_points`
- `league/449.l.246662/...` returned week 1 raw stats and `player_points`
- `league/449.l.246662/settings` returned scoring categories and stat modifiers

### Example league scoring data observed

- stat `4` = Passing Yards, modifier `0.04`
- stat `5` = Passing Touchdowns, modifier `4`
- stat `6` = Interceptions, modifier `-1`
- stat `9` = Rushing Yards, modifier `0.1`
- stat `10` = Rushing Touchdowns, modifier `6`
- stat `11` = Receptions, modifier `0.5`
- stat `12` = Receiving Yards, modifier `0.1`
- stat `13` = Receiving Touchdowns, modifier `6`

### Practical conclusion

For agent experiments, creating a Yahoo league is optional.

- Not required: if agents only need raw weekly player performance data
- Required: if agents need Yahoo's league-specific fantasy points or must act as a real team manager in Yahoo