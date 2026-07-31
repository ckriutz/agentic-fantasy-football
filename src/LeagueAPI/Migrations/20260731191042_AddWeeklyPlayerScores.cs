using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeagueAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddWeeklyPlayerScores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "fantasypros_score_sync_runs",
                columns: table => new
                {
                    SyncRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContainerName = table.Column<string>(type: "character varying(63)", maxLength: 63, nullable: false),
                    BlobName = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    Season = table.Column<int>(type: "integer", nullable: false),
                    EndWeek = table.Column<int>(type: "integer", nullable: false),
                    RetrievedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    BlobETag = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RecordCount = table.Column<int>(type: "integer", nullable: true),
                    MatchedPlayerCount = table.Column<int>(type: "integer", nullable: true),
                    UnmatchedPlayerCount = table.Column<int>(type: "integer", nullable: true),
                    UnmatchedDstCount = table.Column<int>(type: "integer", nullable: true),
                    ServedSeason = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ServedScoring = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    AlreadyProcessed = table.Column<bool>(type: "boolean", nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fantasypros_score_sync_runs", x => x.SyncRunId);
                });

            migrationBuilder.CreateTable(
                name: "weekly_player_scores",
                columns: table => new
                {
                    Season = table.Column<int>(type: "integer", nullable: false),
                    Week = table.Column<int>(type: "integer", nullable: false),
                    FantasyProsPlayerId = table.Column<int>(type: "integer", nullable: false),
                    SleeperPlayerId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PlayerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PositionId = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    TeamId = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Points = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    SyncRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_weekly_player_scores", x => new { x.Season, x.Week, x.FantasyProsPlayerId });
                });

            migrationBuilder.CreateIndex(
                name: "IX_fantasypros_score_sync_runs_ContainerName_BlobName_Season_E~",
                table: "fantasypros_score_sync_runs",
                columns: new[] { "ContainerName", "BlobName", "Season", "EndWeek", "ContentHash" });

            migrationBuilder.CreateIndex(
                name: "IX_fantasypros_score_sync_runs_ContentHash",
                table: "fantasypros_score_sync_runs",
                column: "ContentHash");

            migrationBuilder.CreateIndex(
                name: "IX_fantasypros_score_sync_runs_Season",
                table: "fantasypros_score_sync_runs",
                column: "Season");

            migrationBuilder.CreateIndex(
                name: "IX_fantasypros_score_sync_runs_StartedAtUtc",
                table: "fantasypros_score_sync_runs",
                column: "StartedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_weekly_player_scores_Season_Week",
                table: "weekly_player_scores",
                columns: new[] { "Season", "Week" });

            migrationBuilder.CreateIndex(
                name: "IX_weekly_player_scores_SleeperPlayerId",
                table: "weekly_player_scores",
                column: "SleeperPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_weekly_player_scores_SyncRunId",
                table: "weekly_player_scores",
                column: "SyncRunId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fantasypros_score_sync_runs");

            migrationBuilder.DropTable(
                name: "weekly_player_scores");
        }
    }
}
