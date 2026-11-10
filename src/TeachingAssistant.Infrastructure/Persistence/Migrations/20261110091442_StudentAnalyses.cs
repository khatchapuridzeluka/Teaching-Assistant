using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeachingAssistant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StudentAnalyses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "student_analyses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    course_id = table.Column<string>(type: "text", nullable: false),
                    student_id = table.Column<string>(type: "text", nullable: false),
                    model = table.Column<string>(type: "text", nullable: false),
                    summary = table.Column<string>(type: "text", nullable: false),
                    next_steps = table.Column<string>(type: "text", nullable: false),
                    assignments = table.Column<int>(type: "integer", nullable: false),
                    mistakes = table.Column<int>(type: "integer", nullable: false),
                    strengths = table.Column<int>(type: "integer", nullable: false),
                    records_approved_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_student_analyses", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_student_analyses_course_id_student_id_created_at",
                table: "student_analyses",
                columns: new[] { "course_id", "student_id", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "student_analyses");
        }
    }
}
