using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeagueAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddRosterSlotType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SlotType",
                table: "roster_assignments",
                type: "character varying(8)",
                maxLength: 8,
                nullable: false,
                defaultValue: "BN");

            migrationBuilder.CreateIndex(
                name: "IX_roster_assignments_AgentId_SlotType",
                table: "roster_assignments",
                columns: new[] { "AgentId", "SlotType" },
                unique: true,
                filter: "\"SlotType\" <> 'BN'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_roster_assignments_AgentId_SlotType",
                table: "roster_assignments");

            migrationBuilder.DropColumn(
                name: "SlotType",
                table: "roster_assignments");
        }
    }
}
