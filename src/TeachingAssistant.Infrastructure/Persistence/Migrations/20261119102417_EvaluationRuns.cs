using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeachingAssistant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EvaluationRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "evaluation_runs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    course_id = table.Column<string>(type: "text", nullable: true),
                    models = table.Column<string[]>(type: "text[]", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_evaluation_runs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "evaluation_run_results",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    report_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    model = table.Column<string>(type: "text", nullable: false),
                    answered_model = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    score = table.Column<double>(type: "double precision", nullable: true),
                    max_score = table.Column<double>(type: "double precision", nullable: true),
                    bonus = table.Column<double>(type: "double precision", nullable: true),
                    mistakes = table.Column<string>(type: "jsonb", nullable: false),
                    build_status = table.Column<string>(type: "text", nullable: true),
                    error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    duration_ms = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_evaluation_run_results", x => x.id);
                    table.ForeignKey(
                        name: "fk_evaluation_run_results_evaluation_runs_run_id",
                        column: x => x.run_id,
                        principalTable: "evaluation_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_evaluation_run_results_report_id_model",
                table: "evaluation_run_results",
                columns: new[] { "report_id", "model" });

            migrationBuilder.CreateIndex(
                name: "ix_evaluation_run_results_run_id",
                table: "evaluation_run_results",
                column: "run_id");

            migrationBuilder.CreateIndex(
                name: "ix_evaluation_run_results_status",
                table: "evaluation_run_results",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_evaluation_runs_course_id_created_at",
                table: "evaluation_runs",
                columns: new[] { "course_id", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "evaluation_run_results");

            migrationBuilder.DropTable(
                name: "evaluation_runs");
        }
    }
}
