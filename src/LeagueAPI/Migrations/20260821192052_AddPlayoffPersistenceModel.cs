using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LeagueAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayoffPersistenceModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_matchups_Week_HomeAgentId",
                table: "matchups");

            migrationBuilder.AddColumn<string>(
                name: "MatchupType",
                table: "matchups",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "regular_season");

            migrationBuilder.AddColumn<int>(
                name: "Season",
                table: "matchups",
                type: "integer",
                nullable: false,
                defaultValue: 2025);

            migrationBuilder.AddColumn<string>(
                name: "SeasonStage",
                table: "league_state",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "regular_season");

            migrationBuilder.CreateTable(
                name: "playoff_brackets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Season = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_playoff_brackets", x => x.Id);
                    table.CheckConstraint("CK_playoff_brackets_status", "\"Status\" IN ('projected', 'locked', 'complete')");
                });

            migrationBuilder.CreateTable(
                name: "playoff_settings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    RegularSeasonEndWeek = table.Column<int>(type: "integer", nullable: false),
                    PlayoffStartWeek = table.Column<int>(type: "integer", nullable: false),
                    ChampionshipWeek = table.Column<int>(type: "integer", nullable: false),
                    PlayoffTeamCount = table.Column<int>(type: "integer", nullable: false),
                    FirstRoundByeCount = table.Column<int>(type: "integer", nullable: false),
                    Reseed = table.Column<bool>(type: "boolean", nullable: false),
                    PlayoffTieResolution = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ThirdPlaceGameEnabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_playoff_settings", x => x.Id);
                    table.CheckConstraint("CK_playoff_settings_tie_resolution", "\"PlayoffTieResolution\" IN ('higher_seed')");
                });

            migrationBuilder.CreateTable(
                name: "playoff_bracket_games",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BracketId = table.Column<int>(type: "integer", nullable: false),
                    Round = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    GameSlot = table.Column<int>(type: "integer", nullable: false),
                    Week = table.Column<int>(type: "integer", nullable: false),
                    HomeSeed = table.Column<int>(type: "integer", nullable: true),
                    AwaySeed = table.Column<int>(type: "integer", nullable: true),
                    HomeAgentId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AwayAgentId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    HomeSourceGameId = table.Column<int>(type: "integer", nullable: true),
                    HomeSourceOutcome = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    AwaySourceGameId = table.Column<int>(type: "integer", nullable: true),
                    AwaySourceOutcome = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    MatchupId = table.Column<int>(type: "integer", nullable: true),
                    WinnerAgentId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LoserAgentId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_playoff_bracket_games", x => x.Id);
                    table.CheckConstraint("CK_playoff_bracket_games_away_source_outcome", "\"AwaySourceOutcome\" IS NULL OR \"AwaySourceOutcome\" IN ('winner', 'loser')");
                    table.CheckConstraint("CK_playoff_bracket_games_home_source_outcome", "\"HomeSourceOutcome\" IS NULL OR \"HomeSourceOutcome\" IN ('winner', 'loser')");
                    table.CheckConstraint("CK_playoff_bracket_games_round", "\"Round\" IN ('wild_card', 'semifinal', 'championship', 'third_place')");
                    table.CheckConstraint("CK_playoff_bracket_games_status", "\"Status\" IN ('pending', 'scheduled', 'complete')");
                    table.ForeignKey(
                        name: "FK_playoff_bracket_games_matchups_MatchupId",
                        column: x => x.MatchupId,
                        principalTable: "matchups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_playoff_bracket_games_playoff_bracket_games_AwaySourceGameId",
                        column: x => x.AwaySourceGameId,
                        principalTable: "playoff_bracket_games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_playoff_bracket_games_playoff_bracket_games_HomeSourceGameId",
                        column: x => x.HomeSourceGameId,
                        principalTable: "playoff_bracket_games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_playoff_bracket_games_playoff_brackets_BracketId",
                        column: x => x.BracketId,
                        principalTable: "playoff_brackets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "playoff_seeds",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BracketId = table.Column<int>(type: "integer", nullable: false),
                    Seed = table.Column<int>(type: "integer", nullable: false),
                    AgentId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_playoff_seeds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_playoff_seeds_playoff_brackets_BracketId",
                        column: x => x.BracketId,
                        principalTable: "playoff_brackets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "league_state",
                keyColumn: "Id",
                keyValue: 1,
                column: "SeasonStage",
                value: "regular_season");

            migrationBuilder.InsertData(
                table: "playoff_settings",
                columns: new[] { "Id", "ChampionshipWeek", "FirstRoundByeCount", "PlayoffStartWeek", "PlayoffTeamCount", "PlayoffTieResolution", "RegularSeasonEndWeek", "Reseed", "ThirdPlaceGameEnabled" },
                values: new object[] { 1, 17, 2, 15, 6, "higher_seed", 14, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_matchups_Season_Week_AwayAgentId",
                table: "matchups",
                columns: new[] { "Season", "Week", "AwayAgentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_matchups_Season_Week_HomeAgentId",
                table: "matchups",
                columns: new[] { "Season", "Week", "HomeAgentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_matchups_Season_Week_MatchupType",
                table: "matchups",
                columns: new[] { "Season", "Week", "MatchupType" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_matchups_matchup_type",
                table: "matchups",
                sql: "\"MatchupType\" IN ('regular_season', 'playoff')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_league_state_season_stage",
                table: "league_state",
                sql: "\"SeasonStage\" IN ('draft', 'regular_season', 'playoffs', 'complete')");

            migrationBuilder.CreateIndex(
                name: "IX_playoff_bracket_games_AwaySourceGameId",
                table: "playoff_bracket_games",
                column: "AwaySourceGameId");

            migrationBuilder.CreateIndex(
                name: "IX_playoff_bracket_games_BracketId_Round_GameSlot",
                table: "playoff_bracket_games",
                columns: new[] { "BracketId", "Round", "GameSlot" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_playoff_bracket_games_HomeSourceGameId",
                table: "playoff_bracket_games",
                column: "HomeSourceGameId");

            migrationBuilder.CreateIndex(
                name: "IX_playoff_bracket_games_MatchupId",
                table: "playoff_bracket_games",
                column: "MatchupId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_playoff_brackets_Season",
                table: "playoff_brackets",
                column: "Season",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_playoff_seeds_BracketId_AgentId",
                table: "playoff_seeds",
                columns: new[] { "BracketId", "AgentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_playoff_seeds_BracketId_Seed",
                table: "playoff_seeds",
                columns: new[] { "BracketId", "Seed" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "playoff_bracket_games");

            migrationBuilder.DropTable(
                name: "playoff_seeds");

            migrationBuilder.DropTable(
                name: "playoff_settings");

            migrationBuilder.DropTable(
                name: "playoff_brackets");

            migrationBuilder.DropIndex(
                name: "IX_matchups_Season_Week_AwayAgentId",
                table: "matchups");

            migrationBuilder.DropIndex(
                name: "IX_matchups_Season_Week_HomeAgentId",
                table: "matchups");

            migrationBuilder.DropIndex(
                name: "IX_matchups_Season_Week_MatchupType",
                table: "matchups");

            migrationBuilder.DropCheckConstraint(
                name: "CK_matchups_matchup_type",
                table: "matchups");

            migrationBuilder.DropCheckConstraint(
                name: "CK_league_state_season_stage",
                table: "league_state");

            migrationBuilder.DropColumn(
                name: "MatchupType",
                table: "matchups");

            migrationBuilder.DropColumn(
                name: "Season",
                table: "matchups");

            migrationBuilder.DropColumn(
                name: "SeasonStage",
                table: "league_state");

            migrationBuilder.CreateIndex(
                name: "IX_matchups_Week_HomeAgentId",
                table: "matchups",
                columns: new[] { "Week", "HomeAgentId" });
        }
    }
}
