using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LeagueAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddWeeklyRosterSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsTie",
                table: "matchups",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "WinnerAgentId",
                table: "matchups",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "weekly_roster_snapshots",
                columns: table => new
                {
                    WeeklyRosterSnapshotId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Season = table.Column<int>(type: "integer", nullable: false),
                    Week = table.Column<int>(type: "integer", nullable: false),
                    AgentId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SleeperPlayerId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SlotType = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    IsStarter = table.Column<bool>(type: "boolean", nullable: false),
                    FinalizedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_weekly_roster_snapshots", x => x.WeeklyRosterSnapshotId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_weekly_roster_snapshots_Season_Week_AgentId",
                table: "weekly_roster_snapshots",
                columns: new[] { "Season", "Week", "AgentId" });

            migrationBuilder.CreateIndex(
                name: "IX_weekly_roster_snapshots_Season_Week_AgentId_SleeperPlayerId",
                table: "weekly_roster_snapshots",
                columns: new[] { "Season", "Week", "AgentId", "SleeperPlayerId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "weekly_roster_snapshots");

            migrationBuilder.DropColumn(
                name: "IsTie",
                table: "matchups");

            migrationBuilder.DropColumn(
                name: "WinnerAgentId",
                table: "matchups");
        }
    }
}
