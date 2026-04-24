using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeagueAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddTokenUsageToDecisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CachedInputTokenCount",
                table: "decisions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "InputTokenCount",
                table: "decisions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OutputTokenCount",
                table: "decisions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReasoningTokenCount",
                table: "decisions",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CachedInputTokenCount",
                table: "decisions");

            migrationBuilder.DropColumn(
                name: "InputTokenCount",
                table: "decisions");

            migrationBuilder.DropColumn(
                name: "OutputTokenCount",
                table: "decisions");

            migrationBuilder.DropColumn(
                name: "ReasoningTokenCount",
                table: "decisions");
        }
    }
}
