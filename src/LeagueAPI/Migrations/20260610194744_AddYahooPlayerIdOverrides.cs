using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeagueAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddYahooPlayerIdOverrides : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "yahoo_player_id_overrides",
                columns: table => new
                {
                    YahooPlayerId = table.Column<int>(type: "integer", nullable: false),
                    SleeperPlayerId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_yahoo_player_id_overrides", x => x.YahooPlayerId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_yahoo_player_id_overrides_SleeperPlayerId",
                table: "yahoo_player_id_overrides",
                column: "SleeperPlayerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "yahoo_player_id_overrides");
        }
    }
}
