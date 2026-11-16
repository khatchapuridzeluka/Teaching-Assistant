using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeachingAssistant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GradingBuildCheck : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "build_output",
                table: "gradings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "build_status",
                table: "gradings",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "build_output",
                table: "gradings");

            migrationBuilder.DropColumn(
                name: "build_status",
                table: "gradings");
        }
    }
}
