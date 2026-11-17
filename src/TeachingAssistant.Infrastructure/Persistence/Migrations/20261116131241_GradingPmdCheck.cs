using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeachingAssistant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GradingPmdCheck : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "pmd_findings",
                table: "gradings",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "pmd_output",
                table: "gradings",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "pmd_findings",
                table: "gradings");

            migrationBuilder.DropColumn(
                name: "pmd_output",
                table: "gradings");
        }
    }
}
