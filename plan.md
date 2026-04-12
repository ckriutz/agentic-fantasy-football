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
Sleeper has a free API that allows a JSON download of data of all the players. What we need to do is hit that API, and save the data to a database. Use SQL server here: https://hub.docker.com/r/microsoft/mssql-server

Once this is created, we will create both an API, and an MCP server that will allows the agents to:
- Get a list of players by position.
- Get a list of players by team.
- Get a list of available players.
- Get a player.

### Step 2 - 