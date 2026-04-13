# This is the Agentic Fantasy Football leauge.
The goal is to create a system where AI agents will autonomously play fantasy football live. As this is occuring, I will grab key events and decisions and write about them for fun.

### Key aspects:
- There will be 10 agents, all with differnet backing LLM's from OpenRouter.
- Each agent, when first launched, will define it's own strategy, create it's own team name, and then generate it's own logo. These details will be stored somewhere.
- The Sleeper API will be used to gather all the players. This information will be stored in a database.
- Weekly scores will be retrieved from the Yahoo API, this will give us player points.
- A draft will be conducted, and 17 players will be drafted by each team. The agents will draft players autonomously based on their own strategy.
- Each agent will be given access to sonar as part of perplexity for search, allowing them to keep up with things to help make decisions.
- I will want to simulate the 2025 season as best as I can, over and over again to make sure things work.
- Decisions will be added to a decision log that will allow me to coumb though for interesting things to write about.
- I should create some sort of casual front-end so I can see what's going on.

### Step 1 - Create the players database from sleeper API
Sleeper has a free API that allows a JSON download of data of all the players:

```
https://api.sleeper.app/v1/players/nfl
```

What we need to do is hit that API, and save the data to a database. Use PostgreSQL here:

```
https://hub.docker.com/_/postgres
```

PostgreSQL was chosen over SQL Server because it runs natively on ARM (MacBook M2, Raspberry Pi) without emulation. The .NET ecosystem supports it well via Npgsql / EF Core.

Once this is created, we will create both an API, and an MCP server that will allows the agents to:
- Get a list of players by position.
- Get a list of players by team.
- Get a list of available players.
- Get a player by yahoo id.
- Get a player by sleeper id.
- Get a player by name.

Question... what other endpoints would be useful?

Concern - What if the Sleeper API doesn't contain the Yahoo Id's?
Answer - Sleeper does contain Yahoo Id's, so we should be good. Need to check to make sure they match as part of some tests.

### Step 2 - Create the Yahoo API connection for scores
Yahoo has an API that allows you to get fantasy football scores. We will need to connect to that API and store the scores in our database as well. This will allow us to calculate the points for each player and team.

Once we have the scores, we can use this to calculate the points for each player and team. We will need to create a function that takes in the player ID and the week number, and returns the points for that player for that week.

### Step 3 - Create the agents
We will create 10 agents, each with a different backing LLM from OpenRouter. Each agent will have its own strategy for drafting players and making decisions throughout the season. We will need to define the strategies for each agent, and then implement those strategies in code. Each agent will need to be able to access the player database and the scores database in order to make informed decisions.

Right now, we assume the agents will all be inside the same project, and the main program will trigger them to do things either by event or time driven, or both.

The first time an agent is created, it needs to check to see if it has a strategy, team name, and logo. If it doesn't, it needs to create those things and save them to the database. Once that occurs, we need to ensure this check doesn't occur again.

It needs to be able to have some sort of memory, so it can remember past decisions and outcomes. This will allow it to learn and adapt its strategy over time. This might be done in MD files, or something more exciting like Mem0, though this would be more expensive. We will see how it goes.

Each agent needs to have the following capabilities:
- Define its own strategy for drafting players and making decisions.
- Create its own team name and logo.
- Access it's own list of players.
- Get information about player status through search.
- Access the player database to get information about players.
- Access the scores database to get information about player points.
- Make decisions based on its strategy and the information it has access to.
- When it makes a decision, it should log that decision in a decision log for later analysis.

Questions to answer:
- How will we trigger the agents? Will they run on a schedule, or will they be event-driven?
- In what way will the agents learn and adapt their strategies over time? Will they use some sort of reinforcement learning, or will they simply analyze past decisions and outcomes to make adjustments?
- Where will memory be stored for the agents? Will it be in a database, or will it be in files?

### Step 4 - Create the League System
This is where we will create the system that allows the agents to play against each other. We will need to:

- Create a schedule for the season, and then have the agents play their games according to that schedule.
- Calculate the points for each player and team, and then determine the winner of each game.
- Keep track of the standings throughout the season.
- Track key events and decisions made by the agents, and log those for later analysis and writing.
- Keep track of what players belong to which teams.

### Step 5 - Wire up the Draft
The draft is a key part of the fantasy football season, and we will need to create a system that allows the agents to draft players autonomously. We will need to create a draft order, and then have the agents take turns drafting players according to that order. The agents will need to use their strategies to determine which players to draft, and they will need to access the player database to get information about the players they are considering drafting. We will also need to keep track of which players have been drafted and which players are still available.

Questions:
- How will the agents know it's time for them to draft? Do we send something to them? Do they pull who's turn it is and if it's not their turn they wait and check again in a bit?
- How long will the agents have to make their draft picks? Will there be a time limit for each pick, or will they be able to take as long as they want?

### Step 6 - Mock a season using 2025 data
Once we have all the pieces in place, we will want to simulate the 2025 season using the data we have collected. This will allow us to see how the agents perform against each other, and it will also allow us to identify any issues or bugs in the system. We will want to run multiple simulations of the season to see how the agents perform under different conditions. We will want to analyze the results of the simulations to see if there are any interesting patterns or insights that we can write about. We will also want to use the decision logs to analyze the decisions made by the agents and see if there are any interesting trends or patterns in their decision-making processes.

### Step 7 - Create a front-end to visualize the league
To make it easier to see what's going on in the league, we will want to create a front-end that allows us to visualize the teams, players, scores, and standings. This could be a web application that displays the information in a user-friendly way. We could use a framework like React to build the front-end, and we could use a library like D3.js to create visualizations of the data. The front-end should allow us to see the teams and their players, the scores for each week, and the overall standings in the league. We could also include features that