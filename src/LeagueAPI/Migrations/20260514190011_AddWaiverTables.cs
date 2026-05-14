using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeagueAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddWaiverTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "waiver_claims",
                columns: table => new
                {
                    WaiverClaimId = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Season = table.Column<int>(type: "integer", nullable: false),
                    Week = table.Column<int>(type: "integer", nullable: false),
                    ClaimOrder = table.Column<int>(type: "integer", nullable: false),
                    AddSleeperPlayerId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DropSleeperPlayerId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PriorityAtSubmission = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    FailureReason = table.Column<string>(type: "text", nullable: true),
                    SubmittedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProcessedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_waiver_claims", x => x.WaiverClaimId);
                });

            migrationBuilder.CreateTable(
                name: "waiver_priority",
                columns: table => new
                {
                    AgentId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_waiver_priority", x => x.AgentId);
                });

            migrationBuilder.CreateTable(
                name: "waiver_process_runs",
                columns: table => new
                {
                    WaiverProcessRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    Season = table.Column<int>(type: "integer", nullable: false),
                    Week = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ClaimsProcessed = table.Column<int>(type: "integer", nullable: false),
                    ClaimsSucceeded = table.Column<int>(type: "integer", nullable: false),
                    ClaimsFailed = table.Column<int>(type: "integer", nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_waiver_process_runs", x => x.WaiverProcessRunId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_waiver_claims_Season_Week_AgentId_ClaimOrder",
                table: "waiver_claims",
                columns: new[] { "Season", "Week", "AgentId", "ClaimOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_waiver_claims_Season_Week_Status",
                table: "waiver_claims",
                columns: new[] { "Season", "Week", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_waiver_priority_Priority",
                table: "waiver_priority",
                column: "Priority",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_waiver_process_runs_Season_Week",
                table: "waiver_process_runs",
                columns: new[] { "Season", "Week" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "waiver_claims");

            migrationBuilder.DropTable(
                name: "waiver_priority");

            migrationBuilder.DropTable(
                name: "waiver_process_runs");
        }
    }
}
