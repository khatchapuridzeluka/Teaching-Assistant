using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeachingAssistant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ModelCalls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "model_calls",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    purpose = table.Column<string>(type: "text", nullable: false),
                    requested_model = table.Column<string>(type: "text", nullable: false),
                    model = table.Column<string>(type: "text", nullable: true),
                    succeeded = table.Column<bool>(type: "boolean", nullable: false),
                    error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    input_tokens = table.Column<long>(type: "bigint", nullable: true),
                    output_tokens = table.Column<long>(type: "bigint", nullable: true),
                    cache_read_tokens = table.Column<long>(type: "bigint", nullable: true),
                    cache_write_tokens = table.Column<long>(type: "bigint", nullable: true),
                    duration_ms = table.Column<long>(type: "bigint", nullable: false),
                    course_id = table.Column<string>(type: "text", nullable: true),
                    assignment_id = table.Column<string>(type: "text", nullable: true),
                    version_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_model_calls", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_model_calls_course_id_assignment_id",
                table: "model_calls",
                columns: new[] { "course_id", "assignment_id" });

            migrationBuilder.CreateIndex(
                name: "ix_model_calls_created_at",
                table: "model_calls",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_model_calls_purpose_created_at",
                table: "model_calls",
                columns: new[] { "purpose", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "model_calls");
        }
    }
}
