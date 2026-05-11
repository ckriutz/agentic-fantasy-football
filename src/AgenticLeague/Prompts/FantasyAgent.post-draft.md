# Post-Draft Bootstrap Cleanup

## Purpose

The draft is over. Your bootstrap file was valuable working memory during the draft, but now it contains verbose pick-by-pick notes that are no longer needed in full detail. This task is to clean up your bootstrap file so it reflects your team's current state concisely and is ready for in-season use.

---

## Steps

### 1. Read Your Bootstrap File
Use `ReadAgentBootstrap` to load your current bootstrap content.
Use the `GetMyRoster` tool to load your current roster.

---

### 2. Build a Final Roster Table
Replace the verbose round-by-round draft log with a clean, concise roster table. Use this format:

```
## Final Roster

| Slot | Player | Position | Team | Notes |
|------|--------|----------|------|-------|
| QB   | ...    | QB       | ...  | ...   |
| RB1  | ...    | RB       | ...  | ...   |
| RB2  | ...    | RB       | ...  | ...   |
| WR1  | ...    | WR       | ...  | ...   |
| WR2  | ...    | WR       | ...  | ...   |
| TE   | ...    | TE       | ...  | ...   |
| FLEX | ...    | RB/WR/TE | ...  | ...   |
| K    | ...    | K        | ...  | ...   |
| DEF  | ...    | DEF      | ...  | ...   |
| BN   | ...    | ...      | ...  | ...   |
| BN   | ...    | ...      | ...  | ...   |
| BN   | ...    | ...      | ...  | ...   |
| BN   | ...    | ...      | ...  | ...   |
| BN   | ...    | ...      | ...  | ...   |
| BN   | ...    | ...      | ...  | ...   |
```

The **Notes** column should capture anything that matters going forward: injury concerns, bye week, role uncertainty, upside flag, or handcuff status. Keep it brief (one phrase max).

---

### 3. Optionally Revise Strategy
Review the draft log and ask yourself: did the draft go according to plan, or did the actual picks diverge from the original strategy? If the strategy section in your bootstrap no longer reflects how you actually drafted, update it.

Things to consider:
- Did you end up with a different positional composition than planned (e.g., more QBs, fewer RBs)?
- Are there positional weaknesses on the roster that the strategy should acknowledge?
- Are there specific players to monitor (injury risk, depth chart battles, usage uncertainty)?
- Are there waiver targets or handcuffs to prioritize early in the season?

Only update the strategy if there is something meaningful to change. Keep it focused and actionable — this section is your in-season decision guide, not a recap.

---

### 4. Add an Evolution Log Entry
Append a new entry to the **Evolution Log** at the bottom of the bootstrap file, noting that the post-draft cleanup was completed.

Example entry:
```
- *YYYY-MM-DD:* Post-draft cleanup complete. Draft log condensed to roster table. Strategy reviewed and [updated / confirmed].
```

---

### 5. Write the Updated Bootstrap
Use `WriteAgentBootstrap` to save the updated bootstrap file. The final file should:

- Keep: Team name, logo reference, league settings, strategy, final roster table, evolution log
- Remove: The verbose pick-by-pick draft log (all the "Round X, Pick Y" sections with full rationale text)
- Be concise and ready to use as a quick reference during the regular season
