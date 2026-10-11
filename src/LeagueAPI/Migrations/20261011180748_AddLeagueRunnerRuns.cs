using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LeagueAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddLeagueRunnerRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "league_runner_runs",
                columns: table => new
                {
                    RunnerRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    Mode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Season = table.Column<int>(type: "integer", nullable: true),
                    Week = table.Column<int>(type: "integer", nullable: true),
                    Phase = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Summary = table.Column<string>(type: "text", nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_league_runner_runs", x => x.RunnerRunId);
                });

            migrationBuilder.CreateTable(
                name: "league_runner_agent_results",
                columns: table => new
                {
                    LeagueRunnerAgentResultId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RunnerRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Succeeded = table.Column<bool>(type: "boolean", nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    RecordedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_league_runner_agent_results", x => x.LeagueRunnerAgentResultId);
                    table.ForeignKey(
                        name: "FK_league_runner_agent_results_league_runner_runs_RunnerRunId",
                        column: x => x.RunnerRunId,
                        principalTable: "league_runner_runs",
                        principalColumn: "RunnerRunId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_league_runner_agent_results_RunnerRunId_AgentId",
                table: "league_runner_agent_results",
                columns: new[] { "RunnerRunId", "AgentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_league_runner_runs_StartedAtUtc",
                table: "league_runner_runs",
                column: "StartedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "league_runner_agent_results");

            migrationBuilder.DropTable(
                name: "league_runner_runs");
        }
    }
}
