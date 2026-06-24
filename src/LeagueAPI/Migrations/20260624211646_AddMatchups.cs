using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LeagueAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddMatchups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "matchups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Week = table.Column<int>(type: "integer", nullable: false),
                    HomeAgentId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AwayAgentId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    HomePoints = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    AwayPoints = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    IsComplete = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_matchups", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_matchups_Week_HomeAgentId",
                table: "matchups",
                columns: new[] { "Week", "HomeAgentId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "matchups");
        }
    }
}
