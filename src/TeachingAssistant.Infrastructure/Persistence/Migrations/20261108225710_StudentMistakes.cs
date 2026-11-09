using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeachingAssistant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StudentMistakes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "student_mistakes",
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
                    report_mistake_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_mistake_id = table.Column<Guid>(type: "uuid", nullable: true),
                    file = table.Column<string>(type: "text", nullable: true),
                    start_line = table.Column<int>(type: "integer", nullable: true),
                    end_line = table.Column<int>(type: "integer", nullable: true),
                    severity = table.Column<string>(type: "text", nullable: false),
                    category = table.Column<string>(type: "text", nullable: false),
                    message = table.Column<string>(type: "text", nullable: false),
                    decision = table.Column<string>(type: "text", nullable: false),
                    approved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_student_mistakes", x => x.id);
                    table.ForeignKey(
                        name: "fk_student_mistakes_grading_reports_report_id",
                        column: x => x.report_id,
                        principalTable: "grading_reports",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_student_mistakes_report_mistakes_report_mistake_id",
                        column: x => x.report_mistake_id,
                        principalTable: "report_mistakes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_student_mistakes_course_id_assignment_id",
                table: "student_mistakes",
                columns: new[] { "course_id", "assignment_id" });

            migrationBuilder.CreateIndex(
                name: "ix_student_mistakes_course_id_student_id_approved_at",
                table: "student_mistakes",
                columns: new[] { "course_id", "student_id", "approved_at" });

            migrationBuilder.CreateIndex(
                name: "ix_student_mistakes_report_id",
                table: "student_mistakes",
                column: "report_id");

            migrationBuilder.CreateIndex(
                name: "ix_student_mistakes_report_mistake_id",
                table: "student_mistakes",
                column: "report_mistake_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_student_mistakes_student_id_category",
                table: "student_mistakes",
                columns: new[] { "student_id", "category" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "student_mistakes");
        }
    }
}
