using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeachingAssistant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MistakePoints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "points",
                table: "student_mistakes",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "points",
                table: "report_mistakes",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "points",
                table: "grading_mistakes",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "points",
                table: "student_mistakes");

            migrationBuilder.DropColumn(
                name: "points",
                table: "report_mistakes");

            migrationBuilder.DropColumn(
                name: "points",
                table: "grading_mistakes");
        }
    }
}
