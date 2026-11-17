using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeachingAssistant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GradingStyleCheck : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "style_findings",
                table: "gradings",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "style_output",
                table: "gradings",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "style_findings",
                table: "gradings");

            migrationBuilder.DropColumn(
                name: "style_output",
                table: "gradings");
        }
    }
}
