using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeachingAssistant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GradingReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "grading_reports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    grading_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    score = table.Column<double>(type: "double precision", nullable: true),
                    bonus = table.Column<double>(type: "double precision", nullable: true),
                    review = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    approved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_grading_reports", x => x.id);
                    table.ForeignKey(
                        name: "fk_grading_reports_gradings_grading_id",
                        column: x => x.grading_id,
                        principalTable: "gradings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "report_mistakes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    report_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_mistake_id = table.Column<Guid>(type: "uuid", nullable: true),
                    position = table.Column<int>(type: "integer", nullable: false),
                    file = table.Column<string>(type: "text", nullable: true),
                    start_line = table.Column<int>(type: "integer", nullable: true),
                    end_line = table.Column<int>(type: "integer", nullable: true),
                    severity = table.Column<string>(type: "text", nullable: false),
                    category = table.Column<string>(type: "text", nullable: false),
                    message = table.Column<string>(type: "text", nullable: false),
                    decision = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_report_mistakes", x => x.id);
                    table.ForeignKey(
                        name: "fk_report_mistakes_grading_mistakes_source_mistake_id",
                        column: x => x.source_mistake_id,
                        principalTable: "grading_mistakes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_report_mistakes_grading_reports_report_id",
                        column: x => x.report_id,
                        principalTable: "grading_reports",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_grading_reports_grading_id",
                table: "grading_reports",
                column: "grading_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_grading_reports_status",
                table: "grading_reports",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_grading_reports_version_id",
                table: "grading_reports",
                column: "version_id");

            migrationBuilder.CreateIndex(
                name: "ix_report_mistakes_report_id",
                table: "report_mistakes",
                column: "report_id");

            migrationBuilder.CreateIndex(
                name: "ix_report_mistakes_source_mistake_id",
                table: "report_mistakes",
                column: "source_mistake_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "report_mistakes");

            migrationBuilder.DropTable(
                name: "grading_reports");
        }
    }
}
