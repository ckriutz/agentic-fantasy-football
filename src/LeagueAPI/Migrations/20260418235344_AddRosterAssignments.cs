using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeagueAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddRosterAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "roster_assignments",
                columns: table => new
                {
                    RosterAssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SleeperPlayerId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AcquiredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AcquisitionSource = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roster_assignments", x => x.RosterAssignmentId);
                    table.ForeignKey(
                        name: "FK_roster_assignments_players_SleeperPlayerId",
                        column: x => x.SleeperPlayerId,
                        principalTable: "players",
                        principalColumn: "SleeperPlayerId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_roster_assignments_AgentId",
                table: "roster_assignments",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_roster_assignments_SleeperPlayerId",
                table: "roster_assignments",
                column: "SleeperPlayerId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "roster_assignments");
        }
    }
}
