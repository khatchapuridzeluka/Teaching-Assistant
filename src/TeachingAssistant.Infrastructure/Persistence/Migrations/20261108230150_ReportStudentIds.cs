using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeachingAssistant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReportStudentIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "assignment_id",
                table: "grading_reports",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "course_id",
                table: "grading_reports",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "student_id",
                table: "grading_reports",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("""
                UPDATE grading_reports AS r
                SET student_id = s.student_id, course_id = s.course_id, assignment_id = s.assignment_id
                FROM submission_versions AS v
                JOIN submissions AS s ON s.id = v.submission_id
                WHERE v.id = r.version_id;
                """);

            migrationBuilder.CreateIndex(
                name: "ix_grading_reports_course_id_assignment_id",
                table: "grading_reports",
                columns: new[] { "course_id", "assignment_id" });

            migrationBuilder.CreateIndex(
                name: "ix_grading_reports_course_id_student_id_approved_at",
                table: "grading_reports",
                columns: new[] { "course_id", "student_id", "approved_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_grading_reports_course_id_assignment_id",
                table: "grading_reports");

            migrationBuilder.DropIndex(
                name: "ix_grading_reports_course_id_student_id_approved_at",
                table: "grading_reports");

            migrationBuilder.DropColumn(
                name: "assignment_id",
                table: "grading_reports");

            migrationBuilder.DropColumn(
                name: "course_id",
                table: "grading_reports");

            migrationBuilder.DropColumn(
                name: "student_id",
                table: "grading_reports");
        }
    }
}
