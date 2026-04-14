using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeagueAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddSportsDataEnrichment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AuctionValue",
                table: "players",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AverageDraftPosition",
                table: "players",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ByeWeek",
                table: "players",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FantasyDataId",
                table: "players",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LastSeasonFantasyPoints",
                table: "players",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ProjectedFantasyPoints",
                table: "players",
                type: "numeric",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "sportsdata_fantasy_players",
                columns: table => new
                {
                    SportsDataPlayerId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Team = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Position = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    FantasyPlayerKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AverageDraftPosition = table.Column<decimal>(type: "numeric", nullable: true),
                    AverageDraftPositionPPR = table.Column<decimal>(type: "numeric", nullable: true),
                    ByeWeek = table.Column<int>(type: "integer", nullable: true),
                    LastSeasonFantasyPoints = table.Column<decimal>(type: "numeric", nullable: true),
                    ProjectedFantasyPoints = table.Column<decimal>(type: "numeric", nullable: true),
                    AuctionValue = table.Column<int>(type: "integer", nullable: true),
                    AuctionValuePPR = table.Column<int>(type: "integer", nullable: true),
                    AverageDraftPositionIDP = table.Column<decimal>(type: "numeric", nullable: true),
                    AverageDraftPositionRookie = table.Column<decimal>(type: "numeric", nullable: true),
                    AverageDraftPositionDynasty = table.Column<decimal>(type: "numeric", nullable: true),
                    AverageDraftPosition2QB = table.Column<decimal>(type: "numeric", nullable: true),
                    RawJson = table.Column<string>(type: "text", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sportsdata_fantasy_players", x => x.SportsDataPlayerId);
                });

            migrationBuilder.CreateTable(
                name: "sportsdata_sync_runs",
                columns: table => new
                {
                    SyncRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RecordCount = table.Column<int>(type: "integer", nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sportsdata_sync_runs", x => x.SyncRunId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_players_ByeWeek",
                table: "players",
                column: "ByeWeek");

            migrationBuilder.CreateIndex(
                name: "IX_players_FantasyDataId",
                table: "players",
                column: "FantasyDataId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sportsdata_fantasy_players");

            migrationBuilder.DropTable(
                name: "sportsdata_sync_runs");

            migrationBuilder.DropIndex(
                name: "IX_players_ByeWeek",
                table: "players");

            migrationBuilder.DropIndex(
                name: "IX_players_FantasyDataId",
                table: "players");

            migrationBuilder.DropColumn(
                name: "AuctionValue",
                table: "players");

            migrationBuilder.DropColumn(
                name: "AverageDraftPosition",
                table: "players");

            migrationBuilder.DropColumn(
                name: "ByeWeek",
                table: "players");

            migrationBuilder.DropColumn(
                name: "FantasyDataId",
                table: "players");

            migrationBuilder.DropColumn(
                name: "LastSeasonFantasyPoints",
                table: "players");

            migrationBuilder.DropColumn(
                name: "ProjectedFantasyPoints",
                table: "players");
        }
    }
}
