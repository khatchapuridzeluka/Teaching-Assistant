using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeachingAssistant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StudentPhotos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "photo_content_type",
                table: "students",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "photo_fetched_at",
                table: "students",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "photo_hash",
                table: "students",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "photo_stored_name",
                table: "students",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "photo_url",
                table: "students",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "photo_content_type",
                table: "students");

            migrationBuilder.DropColumn(
                name: "photo_fetched_at",
                table: "students");

            migrationBuilder.DropColumn(
                name: "photo_hash",
                table: "students");

            migrationBuilder.DropColumn(
                name: "photo_stored_name",
                table: "students");

            migrationBuilder.DropColumn(
                name: "photo_url",
                table: "students");
        }
    }
}
