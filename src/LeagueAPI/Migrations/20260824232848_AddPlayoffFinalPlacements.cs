using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeagueAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayoffFinalPlacements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ChampionAgentId",
                table: "playoff_brackets",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FourthPlaceAgentId",
                table: "playoff_brackets",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RunnerUpAgentId",
                table: "playoff_brackets",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ThirdPlaceAgentId",
                table: "playoff_brackets",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChampionAgentId",
                table: "playoff_brackets");

            migrationBuilder.DropColumn(
                name: "FourthPlaceAgentId",
                table: "playoff_brackets");

            migrationBuilder.DropColumn(
                name: "RunnerUpAgentId",
                table: "playoff_brackets");

            migrationBuilder.DropColumn(
                name: "ThirdPlaceAgentId",
                table: "playoff_brackets");
        }
    }
}
