using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeachingAssistant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AssignmentInstructions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "assignment_instructions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source = table.Column<string>(type: "text", nullable: false),
                    course_id = table.Column<string>(type: "text", nullable: false),
                    assignment_id = table.Column<string>(type: "text", nullable: false),
                    storage_folder = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_assignment_instructions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "instructions_versions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    instructions_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    max_points = table.Column<double>(type: "double precision", nullable: true),
                    storage_path = table.Column<string>(type: "text", nullable: false),
                    fetched_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_instructions_versions", x => x.id);
                    table.ForeignKey(
                        name: "fk_instructions_versions_assignment_instructions_instructions_",
                        column: x => x.instructions_id,
                        principalTable: "assignment_instructions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "instructions_contents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    origin = table.Column<string>(type: "text", nullable: false),
                    external_id = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    mime_type = table.Column<string>(type: "text", nullable: true),
                    fingerprint = table.Column<string>(type: "text", nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: true),
                    stored_name = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_instructions_contents", x => x.id);
                    table.ForeignKey(
                        name: "fk_instructions_contents_instructions_versions_version_id",
                        column: x => x.version_id,
                        principalTable: "instructions_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_assignment_instructions_source_assignment_id",
                table: "assignment_instructions",
                columns: new[] { "source", "assignment_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_instructions_contents_version_id",
                table: "instructions_contents",
                column: "version_id");

            migrationBuilder.CreateIndex(
                name: "ix_instructions_versions_instructions_id_number",
                table: "instructions_versions",
                columns: new[] { "instructions_id", "number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "instructions_contents");

            migrationBuilder.DropTable(
                name: "instructions_versions");

            migrationBuilder.DropTable(
                name: "assignment_instructions");
        }
    }
}
