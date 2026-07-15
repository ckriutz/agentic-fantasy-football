using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeagueAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddSportsDataSnapshotImport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AlreadyProcessed",
                table: "sportsdata_sync_runs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "BlobETag",
                table: "sportsdata_sync_runs",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BlobName",
                table: "sportsdata_sync_runs",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContainerName",
                table: "sportsdata_sync_runs",
                type: "character varying(63)",
                maxLength: 63,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContentHash",
                table: "sportsdata_sync_runs",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RetrievedAtUtc",
                table: "sportsdata_sync_runs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_sportsdata_sync_runs_ContainerName_BlobName_RetrievedAtUtc",
                table: "sportsdata_sync_runs",
                columns: new[] { "ContainerName", "BlobName", "RetrievedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_sportsdata_sync_runs_ContentHash",
                table: "sportsdata_sync_runs",
                column: "ContentHash");

            migrationBuilder.CreateIndex(
                name: "IX_sportsdata_sync_runs_StartedAtUtc",
                table: "sportsdata_sync_runs",
                column: "StartedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_sportsdata_sync_runs_ContainerName_BlobName_RetrievedAtUtc",
                table: "sportsdata_sync_runs");

            migrationBuilder.DropIndex(
                name: "IX_sportsdata_sync_runs_ContentHash",
                table: "sportsdata_sync_runs");

            migrationBuilder.DropIndex(
                name: "IX_sportsdata_sync_runs_StartedAtUtc",
                table: "sportsdata_sync_runs");

            migrationBuilder.DropColumn(
                name: "AlreadyProcessed",
                table: "sportsdata_sync_runs");

            migrationBuilder.DropColumn(
                name: "BlobETag",
                table: "sportsdata_sync_runs");

            migrationBuilder.DropColumn(
                name: "BlobName",
                table: "sportsdata_sync_runs");

            migrationBuilder.DropColumn(
                name: "ContainerName",
                table: "sportsdata_sync_runs");

            migrationBuilder.DropColumn(
                name: "ContentHash",
                table: "sportsdata_sync_runs");

            migrationBuilder.DropColumn(
                name: "RetrievedAtUtc",
                table: "sportsdata_sync_runs");
        }
    }
}
