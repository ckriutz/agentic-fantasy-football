<!--
DO NOT INCLUDE THIS COMMENT IN THE DRAFT.
The draft starts at the H1 below. Delete this comment, every other HTML comment, and every unfilled {placeholder}.

This file is the section contract. references/week1.md and references/week2.md are voice only. Do not copy their section order. Week 2's "Standings" heading is actually next week's matchups. Do not imitate that.

Fill rules:
- Two opening teasers, no heading. First is for the technical/AI audience: an agent decision, good or bad. Second is for the fantasy audience: the high score, the blowout, or a player who went off.
- Agent drama: 1 to 3 paragraphs on how the agents reasoned, or the weird mistakes they made. Odd reasoning, superb reasoning, hallucinations, or anything else a technical or AI reader would care about. The heading is a specific title for that story, not the words "Agent Drama".
- Scores table: one row per matchup, schedule order, always 5. Winner on the left. Points to one decimal. Drop a trailing .0 only when it reads cleaner (180, not 180.0). Use an en dash in records elsewhere, not a hyphen.
- Matchups: repeat the matchup block once per game, schedule order, always 5. Heading is "{Winner} vs {Loser}". Bold the result line. Prose has to work for both a fantasy reader and an AI reader. Include who scored, the start/sit, a short quote from the decision log when the log is the story (one from each team is ideal), and whether the swing was bigger than the margin.
- Standings: one row per team, in the order the standings endpoint returns. Record, points for, points against.
- Storylines: 3 to 7 items on what has been happening throughout the season, especially things the agents said they would fix and have not. Questions the next run might actually answer. Do not copy the examples from an older week.
- Next week's matchups: from the next schedule endpoint, not from memory.
- Do not invent Casey's personal fantasy team, his feelings, or a joke he did not make.
-->

# The Agentic Fantasy Football Experiment - Week {week} complete

{Technical/AI teaser. One paragraph.}

{Fantasy teaser. One paragraph.}

### {Agent drama title}

{1 to 3 paragraphs.}

### Now for the scores

<style>
.ff-results{width:100%;border-collapse:collapse;font-family:system-ui,sans-serif;font-size:15px;line-height:1.35;margin:1.25em 0}
.ff-results caption{text-align:left;font-weight:700;font-size:1.05em;margin-bottom:.5em}
.ff-results th{text-align:left;padding:.55em .7em;border-bottom:2px solid #222;font-size:.8em;letter-spacing:.04em;text-transform:uppercase;color:#555}
.ff-results td{padding:.65em .7em;border-bottom:1px solid #e5e5e5;vertical-align:middle}
.ff-results .team{display:flex;align-items:center;gap:.45em}
.ff-results .win{background:#f3fbf4;font-weight:700}
.ff-results .win .score{color:#157347}
.ff-results .lose{color:#444}
.ff-results .score{font-variant-numeric:tabular-nums;text-align:right;white-space:nowrap}
.ff-results .mark{font-size:1.05em}
</style>
<table class="ff-results">
  <caption>Week {week} Scores</caption>
  <thead>
    <tr><th>Winner</th><th class="score">Pts</th><th>Loser</th><th class="score">Pts</th></tr>
  </thead>
  <tbody>
    <!-- Repeat this row once per matchup. Five rows. Schedule order. -->
    <tr>
      <td class="win"><span class="team"><span class="mark">🏆</span>{Winner}</span></td>
      <td class="win score">{winner points}</td>
      <td class="lose">{Loser}</td>
      <td class="lose score">{loser points}</td>
    </tr>
  </tbody>
</table>

<!-- Repeat this block once per matchup, in schedule order. Five blocks. Delete this comment. -->

### {Winner} vs {Loser}

**{Winner} ({agentId}/{model}) beat {Loser} ({agentId}/{model}) {winner points} to {loser points}.**

{How the game went.}

{Who scored. Bold the name and the points. One or two sentences.}

{The decision. What they started, what they sat, what the log said. A short quote if the log is the story.}

{Whether it mattered. The swing math, in a sentence, not a spreadsheet.}

### Where we stand

<style>
.ff-standings{width:100%;border-collapse:collapse;font-family:system-ui,sans-serif;font-size:15px;line-height:1.35;margin:1.25em 0}
.ff-standings caption{text-align:left;font-weight:700;font-size:1.05em;margin-bottom:.5em}
.ff-standings th{text-align:left;padding:.55em .7em;border-bottom:2px solid #222;font-size:.8em;letter-spacing:.04em;text-transform:uppercase;color:#555}
.ff-standings td{padding:.65em .7em;border-bottom:1px solid #e5e5e5;vertical-align:middle}
.ff-standings .num{font-variant-numeric:tabular-nums;text-align:right;white-space:nowrap}
</style>
<table class="ff-standings">
  <caption>Standings</caption>
  <thead>
    <tr><th>Team</th><th>Record</th><th class="num">PF</th><th class="num">PA</th></tr>
  </thead>
  <tbody>
    <!-- Repeat this row once per team, in standings-endpoint order. -->
    <tr>
      <td>{Team}</td>
      <td>{W–L}</td>
      <td class="num">{PF}</td>
      <td class="num">{PA}</td>
    </tr>
  </tbody>
</table>

### Storylines

- {Open question that follows from this week's logs.}
- {Another. 3 to 7 items total.}

### Next Week's Matchups

Week {next week}:

    {Team} vs {Other Team}

    {Team} vs {Other Team}

    {Team} vs {Other Team}

    {Team} vs {Other Team}

    {Team} vs {Other Team}