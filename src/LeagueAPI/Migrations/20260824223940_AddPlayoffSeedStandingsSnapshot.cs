using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeagueAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayoffSeedStandingsSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Losses",
                table: "playoff_seeds",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "PointsAgainst",
                table: "playoff_seeds",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PointsFor",
                table: "playoff_seeds",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "Ties",
                table: "playoff_seeds",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "WinningPercentage",
                table: "playoff_seeds",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "Wins",
                table: "playoff_seeds",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Losses",
                table: "playoff_seeds");

            migrationBuilder.DropColumn(
                name: "PointsAgainst",
                table: "playoff_seeds");

            migrationBuilder.DropColumn(
                name: "PointsFor",
                table: "playoff_seeds");

            migrationBuilder.DropColumn(
                name: "Ties",
                table: "playoff_seeds");

            migrationBuilder.DropColumn(
                name: "WinningPercentage",
                table: "playoff_seeds");

            migrationBuilder.DropColumn(
                name: "Wins",
                table: "playoff_seeds");
        }
    }
}
