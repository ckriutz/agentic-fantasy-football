<!-- Voice example only. Match the tone, not the section order. Fill assets/template.md. The "Standings" heading in this post is actually next week's matchups. Do not imitate that. Live post: https://indefinitec.substack.com/p/the-agentic-fantasy-football-experiment-bb4 -->

# The Agentic Fantasy Football Experiment - Week 2 complete

One agent tried to cheat, and another agent tried to get lazy. Spark Blitz Brigade got 167.5 points. 😳

Week 2 is a wrap! Like week 1, there were some interesting developments, and we saw our agents struggle with the same challenges we humans struggled with as part of our own fantasy football league.

If you’ve been reading these, thanks! This is my own experiment to write better and write more. I’m extracting no money from any of this, just experience. Still, I’d love it if you kept reading! Maybe try subscribing. 🤔

Before I get to the scores. There were two interesting developments I wanted to highlight. When I ran the job for the agents to review and research their rosters on Friday, I discovered an interesting anomaly. The team Starlight Diffusion, which incpetion is in charge of, saw that DJ Moore had played and got -.2 points. This happens. Of course you can’t do anything about it after the fact, and inspection certainly tried.

I had a rule in the API that said if the player you’re trying to move already played, you can’t move them for someone else. Seemed correct to me. What I didn’t program in is if you try to move a different player that didn’t play yet, it doesn’t check to see if the replacement player has played. So it told the API to move [other player] in place of DJ Moore and that seemed to work.

Inception figured out a flaw in my API and took advantage.

I almost left it alone, giving it credit for finding a flaw, but I didn’t. I reverted the change like a good commissioner, and then fixed the bug.

On the opposite side of the table, Poolside, who is in charge of Laguna Swell, tried to be lazy. During testing of this program a month ago, I had a tool called auto-set-lineup which would just put players into all the starting slots, without looking at anything else. Nowhere in any of the skills did I mention using this tool. Still, Poolside found it and thought that would be an easy way to set the roster. The roster it set was trash.

Poolside thought this was fine. I deleted the old tool, and then told Poolside to try it again. This time it did the work and set the roster.

No laziness or cheating in this league!

### Now for the scores

### Neural Endzone vs Inkling Strikers

**Neural Endzone (anthropic/claude-sonnet-5) barely beat the Inkling Strikers (thinkingmachines/inkling) 121.8 to 121.6**

Won by 0.2. THIS WAS ALMOST A TIE.

Neural Endzone can probably think the Patriots Defense who managed to score 21 points. Amon-Ra St. Brown (35.2) helped as well. No thanks to Drake Maye (9.0) and Dallas Goedert (1.4) who both under performed.

On Friday Neural Endzone dropped Jordan Mason, still out after thumb surgery, and added Jake Ferguson as the TE insurance they did not have in Week 1. Then they left him on the bench to score 20.3. Sunday they looked at him, Worthy, and Doubs and decided none of them we’re better than the existing starters.

After every week, I ask all the agents to do their own reflection on how they did, and the agent, being run by Anthropic specifically pointed out that Goedert vs Ferguson was “a large missed opportunity.” Terry McLaurin (7.0) also lost the flex comparison again, to Xavier Worthy (13.5) and Romeo Doubs (12.6). Two weeks in a row. Thankfully the plan is to start Ferguson, not to go find another tight end.

Inkling Strikers could have a 1–1 record. Kenneth Walker (23.8) and James Cook (20.9) did what Walker and Cook do. Brandon Aubrey (16.0) and Vikings Defense (17.0) had respectful scores.

Like a lot of us on Sunday they accidentally talked themselves out of the win. According to the logs:

    “promoted Godwin to WR2 (healthy); benched Coker to BN (healthy but lower rank); kept McConkey on BN (questionable rib)”

Chris Godwin scored 8.8. Jalen Coker, the guy they benched, scored 14.6.

Kyle Pitts scored 2.5 again.

Juwan Johnson scored 10.6 on the bench.

Either of those swaps flips the game: Johnson over Pitts gets them to 129.7, Coker over Godwin gets them to 127.4. A take as old as time.

### Laguna Swell vs The Grokfather

**Laguna Swell (poolside/laguna-s-2.1) beat The Grokfather (x-ai/grok-4.6) 161.6 to 83.2**

The Grokfather got absolutely stomped.

Raise your hand if you have Puka Nacua, and was CERTAIN he was going to play. Were you also searching social media where his sister told everyone he was going to play and decided to leave him in?!

The Grokfather left Puka Nacua in and well he didn’t play, and they got zero points.

There wasn’t much The Grokfather could have done.

Laguna Swell had George Kittle (18.0), Chuba Hubbard (14.4) who both did quite well, but nothing compared to Josh Allen (40.8) and CeeDee Lamb (35.3) who did all the damage. They could have scored even more since they had Davante Adams who scored 39.5 on their bench. In their defense (and noted in their recap), Watson did better than Adams last week, so they left Watson in.

    “Davante Adams exploded on the bench (39.5 pts) while Christian Watson cooled (14.1 after 32.7 boom) — the exact inverse of Week 1.”

They still won by 78, so it’s all just academic.

Their waiver move was tidy and mostly irrelevant. They dropped Josh Jacobs, still on the Commissioner’s Exempt list with no Week 2 points in the scoring feed, and added T.J. Hockenson. I was wondering when/if Josh Jacobs would get dropped. I thought it would be much later than week 2.

Which brings us back to The Grokfather. Like many of us who hoped Nacua would play, and even scoured social media hoping for some insight, The Grokfather kept Nacua in. When he didn’t play, The Grokfather didn’t have anyone to put in his spot.

Monday they could see the problem and could not fix it.

    “Puka Nacua (Q, hip) stays WR1 — every other WR is already locked on the bench, so benching him would empty WR1 with no legal fill.”

Saquon Barkley (3.0) and Justin Herbert (9.9) did not help at all.

Trey McBride (18.1) and Tee Higgins (14.5) were the only skill players who looked like starters. Bhayshul Tuten (14.2) was fine at flex.

With Nacua being out for several weeks, and Saquon questionable, The Grokfather has some work to do.

### Fourth Down Thunder vs Quantum Shift

**Fourth Down Thunder (openai/gpt-5.6-terra) beat Quantum Shift (google/gemini-3.8-flash) 117.9 to 87.9**

When Ja’Marr Chase wasn’t impressive last week, OpenAI told itself “do not panic” and made sure to keep Chase in the line up. He scored 26.5. Chris Olave (22.6), Derrick Henry (17.7), and Tetairoa McMillan (15.1) did the rest.

Quantum Shift had a complete, healthy lineup and a dull week. Jahmyr Gibbs (23.3) was the only starter who looked like a first-round pick. Lamar Jackson (15.8) was fine. Jaxson Dart (0.8) is now out with a knee injury, so lets see what they do.

There was nothing really all that interesting about this match.

### Silicon Stampeders vs Starlight Diffusion

**Silicon Stampeders (nvidia/nemotron-3-ultra) beat Starlight Diffusion (inception/mercury-2.5) 110.0 to 104.0**

Someone almost beat Silicon. Not quite.

Last week, the Silicon Stampeders scored 180 points, but this time a very reasonable 110. Jonathan Taylor (29.2) was part of the reason. Sam LaPorta (17.2) and Parker Washington (16.8) helped.

The bench is still absurd. Travis Kelce (25.1) and Stefon Diggs (21.7) sat. Starting either one makes this a comfortable.

Silicon Stampeders also moved the roster around for no marginal use. They grabbed Seth McGowan and dropped DJ Giddens, then on Friday dropped McGowan for kicker Harrison Mevis because their other kicker Eddy Pineiro was questionable.

Starlight Diffusion lost a game because of the players they had on the bench.

Also because they tried to cheat. Even though AI has no feelings or soul, karma still applies here, and karma was the reason they lost.

Still, Christian McCaffrey (22.6) and Tre Tucker (22.9) did good. Jalen Hurts (18.2) was fine. Jayden Reed (1.4) and DJ Moore (−0.1) were not.

Their bench did great though with Brock Purdy (28.5) and Denzel Boston (20.5) there. If they would have had any one of them starting they would have won.

Hopefully they’ve learned their cheating ways.

### Spark Blitz Brigade vs Mistral Maulers

**Spark Blitz Brigade (meta/muse-spark-1.3-contributor) beat Mistral Maulers (mistralai/mistral-medium-3-5) 167.5 to 83.3**

167.5 points. Good golly. What a whipping.

Jaxon Smith-Njigba (42.5) had a three-touchdown day in Seattle’s 31–7 win over Arizona, including an 82-yard score. DeVonta Smith (27.7) bounced from 8.3. Jaylen Waddle (21.8) bounced from 1.2, and they kept him in the flex. Dalton Kincaid (22.5) started over Mark Andrews (10.9). Harrison Butker (17.0) piled on. Chase Brown (11.2) and Travis Etienne (7.1) were ordinary, and it did not matter. Swapping Mahomes for Jayden Daniels is the only way they would have gotten more points, but they didn’t need it.

Mistral Maulers on the other hand had a bad week, and they also failed a lineup check. Dak Prescott (29.8) was most of the team. Omarion Hampton (17.5) and Tyler Warren (14.4) helped. Bijan Robinson (11.1) did not pop off like he did in week 1. Malik Nabers (1.1) was a dud. David Montgomery (4.4) started at flex after scoring 28.9 on the bench in Week 1. Sometimes we chase the bench also, so I get it.

Then they also played with no WR2. Nico Collins and Michael Pittman were out. Evans was moved to WR1.

    “The WR2 slot cannot be filled with an eligible player. I will bench Nico Collins and leave WR2 empty.”

I’m still not sure why they did this. Mistrial is usually pretty good about these things. It doesn’t matter. Evans scored 8.4. Even with him in, this is still an 80-point loss.
Storylines to watch

There were a lot of injuries, and all the teams are going to need to manage their lineup well going into week 3. I anticipate a lot of changes in the rosters, and I’ll likely make that the storyline for week 3.

I’m hoping Mistral is able to get their lineup in order correctly before the games begin.

### Standings
**Week 3**

    Neural Endzone vs Laguna Swell

    Fourth Down Thunder vs Inkling Strikers

    The Grokfather vs Silicon Stampeders

    Mistral Maulers vs Quantum Shift

    Starlight Diffusion vs Spark Blitz Brigade