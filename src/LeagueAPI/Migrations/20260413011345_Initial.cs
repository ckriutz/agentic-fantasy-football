using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeagueAPI.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "players",
                columns: table => new
                {
                    SleeperPlayerId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    YahooId = table.Column<int>(type: "integer", nullable: true),
                    FullName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SearchFullNameNormalized = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Team = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    TeamAbbr = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Position = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    FantasyPositionsTokenized = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    Sport = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    RawJson = table.Column<string>(type: "text", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_players", x => x.SleeperPlayerId);
                });

            migrationBuilder.CreateTable(
                name: "sleeper_sync_runs",
                columns: table => new
                {
                    SyncRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RecordCount = table.Column<int>(type: "integer", nullable: true),
                    SnapshotFileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: true),
                    SnapshotRelativePath = table.Column<string>(type: "character varying(520)", maxLength: 520, nullable: true),
                    PayloadSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sleeper_sync_runs", x => x.SyncRunId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_players_SearchFullNameNormalized",
                table: "players",
                column: "SearchFullNameNormalized");

            migrationBuilder.CreateIndex(
                name: "IX_players_TeamAbbr_Position",
                table: "players",
                columns: new[] { "TeamAbbr", "Position" });

            migrationBuilder.CreateIndex(
                name: "IX_players_YahooId",
                table: "players",
                column: "YahooId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "players");

            migrationBuilder.DropTable(
                name: "sleeper_sync_runs");
        }
    }
}
