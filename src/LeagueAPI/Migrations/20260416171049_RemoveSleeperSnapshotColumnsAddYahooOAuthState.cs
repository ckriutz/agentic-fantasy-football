using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeagueAPI.Migrations
{
    /// <inheritdoc />
    public partial class RemoveSleeperSnapshotColumnsAddYahooOAuthState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PayloadSha256",
                table: "sleeper_sync_runs");

            migrationBuilder.DropColumn(
                name: "SnapshotFileName",
                table: "sleeper_sync_runs");

            migrationBuilder.DropColumn(
                name: "SnapshotRelativePath",
                table: "sleeper_sync_runs");

            migrationBuilder.CreateTable(
                name: "yahoo_oauth_state",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    AccessToken = table.Column<string>(type: "text", nullable: true),
                    RefreshToken = table.Column<string>(type: "text", nullable: true),
                    TokenType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ExpiresInSeconds = table.Column<int>(type: "integer", nullable: true),
                    IssuedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AccessTokenExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastRefreshedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Scope = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AuthorizationState = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_yahoo_oauth_state", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "yahoo_oauth_state");

            migrationBuilder.AddColumn<string>(
                name: "PayloadSha256",
                table: "sleeper_sync_runs",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SnapshotFileName",
                table: "sleeper_sync_runs",
                type: "character varying(260)",
                maxLength: 260,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SnapshotRelativePath",
                table: "sleeper_sync_runs",
                type: "character varying(520)",
                maxLength: 520,
                nullable: true);
        }
    }
}
