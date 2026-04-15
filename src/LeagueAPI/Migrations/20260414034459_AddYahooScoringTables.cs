using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LeagueAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddYahooScoringTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "scoring_templates",
                columns: table => new
                {
                    TemplateKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scoring_templates", x => x.TemplateKey);
                });

            migrationBuilder.CreateTable(
                name: "yahoo_sync_runs",
                columns: table => new
                {
                    SyncRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    GameKey = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Season = table.Column<int>(type: "integer", nullable: false),
                    Week = table.Column<int>(type: "integer", nullable: true),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PageCount = table.Column<int>(type: "integer", nullable: true),
                    RecordCount = table.Column<int>(type: "integer", nullable: true),
                    MatchedPlayerCount = table.Column<int>(type: "integer", nullable: true),
                    UnmatchedPlayerCount = table.Column<int>(type: "integer", nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_yahoo_sync_runs", x => x.SyncRunId);
                });

            migrationBuilder.CreateTable(
                name: "scoring_template_rules",
                columns: table => new
                {
                    TemplateKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    StatId = table.Column<int>(type: "integer", nullable: false),
                    StatName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Modifier = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scoring_template_rules", x => new { x.TemplateKey, x.StatId });
                    table.ForeignKey(
                        name: "FK_scoring_template_rules_scoring_templates_TemplateKey",
                        column: x => x.TemplateKey,
                        principalTable: "scoring_templates",
                        principalColumn: "TemplateKey",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "weekly_player_stats",
                columns: table => new
                {
                    WeeklyPlayerStatId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GameKey = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Season = table.Column<int>(type: "integer", nullable: false),
                    Week = table.Column<int>(type: "integer", nullable: false),
                    YahooPlayerId = table.Column<int>(type: "integer", nullable: false),
                    SleeperPlayerId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    FullName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Team = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Position = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    EditorialTeamAbbr = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    SyncRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    RawJson = table.Column<string>(type: "text", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_weekly_player_stats", x => x.WeeklyPlayerStatId);
                    table.ForeignKey(
                        name: "FK_weekly_player_stats_yahoo_sync_runs_SyncRunId",
                        column: x => x.SyncRunId,
                        principalTable: "yahoo_sync_runs",
                        principalColumn: "SyncRunId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "weekly_player_stat_values",
                columns: table => new
                {
                    WeeklyPlayerStatId = table.Column<long>(type: "bigint", nullable: false),
                    StatId = table.Column<int>(type: "integer", nullable: false),
                    StatName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Value = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_weekly_player_stat_values", x => new { x.WeeklyPlayerStatId, x.StatId });
                    table.ForeignKey(
                        name: "FK_weekly_player_stat_values_weekly_player_stats_WeeklyPlayerS~",
                        column: x => x.WeeklyPlayerStatId,
                        principalTable: "weekly_player_stats",
                        principalColumn: "WeeklyPlayerStatId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "weekly_player_points",
                columns: table => new
                {
                    WeeklyPlayerPointId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    WeeklyPlayerStatId = table.Column<long>(type: "bigint", nullable: false),
                    TemplateKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FantasyPoints = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    BreakdownJson = table.Column<string>(type: "text", nullable: true),
                    CalculatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_weekly_player_points", x => x.WeeklyPlayerPointId);
                    table.ForeignKey(
                        name: "FK_weekly_player_points_scoring_templates_TemplateKey",
                        column: x => x.TemplateKey,
                        principalTable: "scoring_templates",
                        principalColumn: "TemplateKey",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_weekly_player_points_weekly_player_stats_WeeklyPlayerStatId",
                        column: x => x.WeeklyPlayerStatId,
                        principalTable: "weekly_player_stats",
                        principalColumn: "WeeklyPlayerStatId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_scoring_templates_IsActive",
                table: "scoring_templates",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_weekly_player_points_TemplateKey",
                table: "weekly_player_points",
                column: "TemplateKey");

            migrationBuilder.CreateIndex(
                name: "IX_weekly_player_points_WeeklyPlayerStatId_TemplateKey",
                table: "weekly_player_points",
                columns: new[] { "WeeklyPlayerStatId", "TemplateKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_weekly_player_stat_values_StatId",
                table: "weekly_player_stat_values",
                column: "StatId");

            migrationBuilder.CreateIndex(
                name: "IX_weekly_player_stats_Season_Week_Position",
                table: "weekly_player_stats",
                columns: new[] { "Season", "Week", "Position" });

            migrationBuilder.CreateIndex(
                name: "IX_weekly_player_stats_Season_Week_YahooPlayerId",
                table: "weekly_player_stats",
                columns: new[] { "Season", "Week", "YahooPlayerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_weekly_player_stats_SleeperPlayerId",
                table: "weekly_player_stats",
                column: "SleeperPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_weekly_player_stats_SyncRunId",
                table: "weekly_player_stats",
                column: "SyncRunId");

            migrationBuilder.CreateIndex(
                name: "IX_yahoo_sync_runs_GameKey_Season_Week",
                table: "yahoo_sync_runs",
                columns: new[] { "GameKey", "Season", "Week" });

            migrationBuilder.CreateIndex(
                name: "IX_yahoo_sync_runs_StartedAtUtc",
                table: "yahoo_sync_runs",
                column: "StartedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "scoring_template_rules");

            migrationBuilder.DropTable(
                name: "weekly_player_points");

            migrationBuilder.DropTable(
                name: "weekly_player_stat_values");

            migrationBuilder.DropTable(
                name: "scoring_templates");

            migrationBuilder.DropTable(
                name: "weekly_player_stats");

            migrationBuilder.DropTable(
                name: "yahoo_sync_runs");
        }
    }
}
