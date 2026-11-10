using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeachingAssistant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Strengths : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "grading_strengths",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    grading_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    file = table.Column<string>(type: "text", nullable: true),
                    start_line = table.Column<int>(type: "integer", nullable: true),
                    end_line = table.Column<int>(type: "integer", nullable: true),
                    category = table.Column<string>(type: "text", nullable: false),
                    message = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_grading_strengths", x => x.id);
                    table.ForeignKey(
                        name: "fk_grading_strengths_gradings_grading_id",
                        column: x => x.grading_id,
                        principalTable: "gradings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "report_strengths",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    report_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_strength_id = table.Column<Guid>(type: "uuid", nullable: true),
                    position = table.Column<int>(type: "integer", nullable: false),
                    file = table.Column<string>(type: "text", nullable: true),
                    start_line = table.Column<int>(type: "integer", nullable: true),
                    end_line = table.Column<int>(type: "integer", nullable: true),
                    category = table.Column<string>(type: "text", nullable: false),
                    message = table.Column<string>(type: "text", nullable: false),
                    decision = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_report_strengths", x => x.id);
                    table.ForeignKey(
                        name: "fk_report_strengths_grading_reports_report_id",
                        column: x => x.report_id,
                        principalTable: "grading_reports",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_report_strengths_grading_strengths_source_strength_id",
                        column: x => x.source_strength_id,
                        principalTable: "grading_strengths",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "student_strengths",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<string>(type: "text", nullable: false),
                    course_id = table.Column<string>(type: "text", nullable: false),
                    assignment_id = table.Column<string>(type: "text", nullable: false),
                    submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    grading_id = table.Column<Guid>(type: "uuid", nullable: false),
                    report_id = table.Column<Guid>(type: "uuid", nullable: false),
                    report_strength_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_strength_id = table.Column<Guid>(type: "uuid", nullable: true),
                    file = table.Column<string>(type: "text", nullable: true),
                    start_line = table.Column<int>(type: "integer", nullable: true),
                    end_line = table.Column<int>(type: "integer", nullable: true),
                    category = table.Column<string>(type: "text", nullable: false),
                    message = table.Column<string>(type: "text", nullable: false),
                    decision = table.Column<string>(type: "text", nullable: false),
                    approved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_student_strengths", x => x.id);
                    table.ForeignKey(
                        name: "fk_student_strengths_grading_reports_report_id",
                        column: x => x.report_id,
                        principalTable: "grading_reports",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_student_strengths_report_strengths_report_strength_id",
                        column: x => x.report_strength_id,
                        principalTable: "report_strengths",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_grading_strengths_grading_id",
                table: "grading_strengths",
                column: "grading_id");

            migrationBuilder.CreateIndex(
                name: "ix_report_strengths_report_id",
                table: "report_strengths",
                column: "report_id");

            migrationBuilder.CreateIndex(
                name: "ix_report_strengths_source_strength_id",
                table: "report_strengths",
                column: "source_strength_id");

            migrationBuilder.CreateIndex(
                name: "ix_student_strengths_course_id_assignment_id",
                table: "student_strengths",
                columns: new[] { "course_id", "assignment_id" });

            migrationBuilder.CreateIndex(
                name: "ix_student_strengths_course_id_student_id_approved_at",
                table: "student_strengths",
                columns: new[] { "course_id", "student_id", "approved_at" });

            migrationBuilder.CreateIndex(
                name: "ix_student_strengths_report_id",
                table: "student_strengths",
                column: "report_id");

            migrationBuilder.CreateIndex(
                name: "ix_student_strengths_report_strength_id",
                table: "student_strengths",
                column: "report_strength_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_student_strengths_student_id_category",
                table: "student_strengths",
                columns: new[] { "student_id", "category" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "student_strengths");

            migrationBuilder.DropTable(
                name: "report_strengths");

            migrationBuilder.DropTable(
                name: "grading_strengths");
        }
    }
}
