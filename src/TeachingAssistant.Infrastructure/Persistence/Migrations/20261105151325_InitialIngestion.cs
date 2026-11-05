using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeachingAssistant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialIngestion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "submissions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source = table.Column<string>(type: "text", nullable: false),
                    course_id = table.Column<string>(type: "text", nullable: false),
                    assignment_id = table.Column<string>(type: "text", nullable: false),
                    student_id = table.Column<string>(type: "text", nullable: false),
                    student_name = table.Column<string>(type: "text", nullable: false),
                    external_submission_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_submissions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "submission_versions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    status_detail = table.Column<string>(type: "text", nullable: true),
                    content_hash = table.Column<string>(type: "text", nullable: true),
                    storage_path = table.Column<string>(type: "text", nullable: false),
                    turned_in_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    fetched_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_submission_versions", x => x.id);
                    table.ForeignKey(
                        name: "fk_submission_versions_submissions_submission_id",
                        column: x => x.submission_id,
                        principalTable: "submissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "version_contents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    origin = table.Column<string>(type: "text", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    external_id = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    mime_type = table.Column<string>(type: "text", nullable: true),
                    fingerprint = table.Column<string>(type: "text", nullable: false),
                    @ref = table.Column<string>(name: "ref", type: "text", nullable: true),
                    size_bytes = table.Column<long>(type: "bigint", nullable: true),
                    stored_name = table.Column<string>(type: "text", nullable: false),
                    llm_ready_name = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_version_contents", x => x.id);
                    table.ForeignKey(
                        name: "fk_version_contents_submission_versions_version_id",
                        column: x => x.version_id,
                        principalTable: "submission_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_submission_versions_submission_id_number",
                table: "submission_versions",
                columns: new[] { "submission_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_submissions_assignment_id",
                table: "submissions",
                column: "assignment_id");

            migrationBuilder.CreateIndex(
                name: "ix_submissions_source_external_submission_id",
                table: "submissions",
                columns: new[] { "source", "external_submission_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_version_contents_version_id",
                table: "version_contents",
                column: "version_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "version_contents");

            migrationBuilder.DropTable(
                name: "submission_versions");

            migrationBuilder.DropTable(
                name: "submissions");
        }
    }
}
