using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeagueAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddFantasyProsSnapshotImport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "fantasypros_ranking_players",
                columns: table => new
                {
                    PlayerId = table.Column<int>(type: "integer", nullable: false),
                    PlayerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SportsDataId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PlayerTeamId = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    PlayerPositionId = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    PlayerPositions = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PlayerShortName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PlayerEligibility = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PlayerYahooPositions = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PlayerPageUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    PlayerFilename = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    PlayerYahooId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CbsPlayerId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PlayerByeWeek = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    PlayerOwnedAverage = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    PlayerOwnedEspn = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    PlayerOwnedYahoo = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    PlayerEcrDelta = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    RankEcr = table.Column<int>(type: "integer", nullable: false),
                    RankMinimum = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    RankMaximum = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    RankAverage = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    RankStandardDeviation = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    PositionRank = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Tier = table.Column<int>(type: "integer", nullable: false),
                    Season = table.Column<int>(type: "integer", nullable: false),
                    Week = table.Column<int>(type: "integer", nullable: false),
                    RetrievedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RawJson = table.Column<string>(type: "text", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fantasypros_ranking_players", x => x.PlayerId);
                });

            migrationBuilder.CreateTable(
                name: "fantasypros_sync_runs",
                columns: table => new
                {
                    SyncRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContainerName = table.Column<string>(type: "character varying(63)", maxLength: 63, nullable: false),
                    BlobName = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    Season = table.Column<int>(type: "integer", nullable: false),
                    Week = table.Column<int>(type: "integer", nullable: false),
                    RetrievedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    BlobETag = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RecordCount = table.Column<int>(type: "integer", nullable: true),
                    AlreadyProcessed = table.Column<bool>(type: "boolean", nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fantasypros_sync_runs", x => x.SyncRunId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_fantasypros_ranking_players_PlayerYahooId",
                table: "fantasypros_ranking_players",
                column: "PlayerYahooId");

            migrationBuilder.CreateIndex(
                name: "IX_fantasypros_ranking_players_Season_Week",
                table: "fantasypros_ranking_players",
                columns: new[] { "Season", "Week" });

            migrationBuilder.CreateIndex(
                name: "IX_fantasypros_ranking_players_SportsDataId",
                table: "fantasypros_ranking_players",
                column: "SportsDataId");

            migrationBuilder.CreateIndex(
                name: "IX_fantasypros_sync_runs_ContainerName_BlobName_Season_Week_Re~",
                table: "fantasypros_sync_runs",
                columns: new[] { "ContainerName", "BlobName", "Season", "Week", "RetrievedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_fantasypros_sync_runs_ContentHash",
                table: "fantasypros_sync_runs",
                column: "ContentHash");

            migrationBuilder.CreateIndex(
                name: "IX_fantasypros_sync_runs_StartedAtUtc",
                table: "fantasypros_sync_runs",
                column: "StartedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fantasypros_ranking_players");

            migrationBuilder.DropTable(
                name: "fantasypros_sync_runs");
        }
    }
}
