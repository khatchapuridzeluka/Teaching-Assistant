using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeachingAssistant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GradingDiscardReason : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "discard_note",
                table: "gradings",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "discard_reason",
                table: "gradings",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "discard_note",
                table: "gradings");

            migrationBuilder.DropColumn(
                name: "discard_reason",
                table: "gradings");
        }
    }
}
