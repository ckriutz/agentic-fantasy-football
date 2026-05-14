# Post-Draft Bootstrap Cleanup

## Purpose

The draft is over. Your bootstrap file was valuable working memory during the draft, but now it contains verbose pick-by-pick notes that are no longer needed in full detail. This task is to clean up your bootstrap file so it reflects your team's current state concisely and is ready for in-season use.

Remember, this is **your** team, so any responses should come from that point of view.

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

| Slot  | Player | Position | Team | Notes |
|-------|--------|----------|------|-------|
| QB1   | ...    | QB       | ...  | ...   |
| RB1   | ...    | RB       | ...  | ...   |
| RB2   | ...    | RB       | ...  | ...   |
| WR1   | ...    | WR       | ...  | ...   |
| WR2   | ...    | WR       | ...  | ...   |
| TE1   | ...    | TE       | ...  | ...   |
| FLEX1 | ...    | RB/WR/TE | ...  | ...   |
| K1    | ...    | K        | ...  | ...   |
| DEF1  | ...    | DEF      | ...  | ...   |
| BN    | ...    | ...      | ...  | ...   |
| BN    | ...    | ...      | ...  | ...   |
| BN    | ...    | ...      | ...  | ...   |
| BN    | ...    | ...      | ...  | ...   |
| BN    | ...    | ...      | ...  | ...   |
| BN    | ...    | ...      | ...  | ...   |
```

The **Notes** column should capture anything that matters going forward: injury concerns, bye week, role uncertainty, upside flag, or handcuff status. Keep it brief (one phrase max).

---

### 3. Use Tools to Officially Update Your Roster
First, call `GetMyRoster` to see all your players and their current `slotType` assignments. Each player in the response includes a `sleeperPlayerId` — you will need this ID when calling `SetPlayerSlot`.

Use the `SetPlayerSlot` tool for each starting slot. The valid slot values are: **QB1, RB1, RB2, WR1, WR2, TE1, FLEX1, K1, DEF1, BN**. You must use these exact slot names (including the number suffix).

A few rules to follow:
- Players can only be placed in slots that match their position eligibility. A WR cannot be placed in an RB slot, for example. FLEX1 accepts RB, WR, or TE.
- For the FLEX1 slot, only eligible RBs, WRs, and TEs not already in a starting slot can be assigned to the FLEX1 spot.

How to determine the best player for the position:
- Compare the `searchRank`. The lower the `searchRank` the better.
- Compare the `depth_chart_order`, The lower the `depth_chart_order` the better.
- Compare the `projectedFantasyPoints`. the higher the `projectedFantasyPoints` the better.
- Compare the `auctionValue`. The higher the `auctionValue` the better.
- Make sure `active` is True.
- Look at `averageDraftPosition`. The lower the number the better.
- Compare the `acquiredAtUtc`. This gives you and idea about who you drafted earlier, and in theory that player is a better choice to start.

There is no simple way to determine which player should start, so use the metrics for your best decision.

Use the `GetMyRoster` tool after assigning slots to verify your current roster assignments. Ensure all starting slots are filled. Keep iterating until everything is set correctly. If you still do not have your starting positions filled, continue to use the `SetPlayerSlot` tool to move players from the bench to a starting slot.

---

### 4. Optionally Revise Strategy
Review the draft log and ask yourself: did the draft go according to plan, or did the actual picks diverge from the original strategy? If the strategy section in your bootstrap no longer reflects how you actually drafted, update it.

Things to consider:
- Did you end up with a different positional composition than planned (e.g., more QBs, fewer RBs)?
- Are there positional weaknesses on the roster that the strategy should acknowledge?
- Are there specific players to monitor (injury risk, depth chart battles, usage uncertainty)?
- Are there waiver targets or handcuffs to prioritize early in the season?

Only update the strategy if there is something meaningful to change. Keep it focused and actionable — this section is your in-season decision guide, not a recap.

---

### 5. Add an Evolution Log Entry
Append a new entry to the **Evolution Log** at the bottom of the bootstrap file, noting that the post-draft cleanup was completed.

Example entry:
```
- *YYYY-MM-DD:* Post-draft cleanup complete. Draft log condensed to roster table. Strategy reviewed and [updated / confirmed].
```

---

### 6. Write the Updated Bootstrap
Use `WriteAgentBootstrap` to save the updated bootstrap file. The final file should:

- Keep: Team name, logo reference, league settings, strategy, final roster table, evolution log
- Remove: The verbose pick-by-pick draft log (all the "Round X, Pick Y" sections with full rationale text)
- Be concise and ready to use as a quick reference during the regular season
