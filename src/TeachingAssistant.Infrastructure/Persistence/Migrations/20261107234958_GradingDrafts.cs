using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeachingAssistant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GradingDrafts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "gradings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    model = table.Column<string>(type: "text", nullable: false),
                    review = table.Column<string>(type: "text", nullable: false),
                    score = table.Column<double>(type: "double precision", nullable: true),
                    max_score = table.Column<double>(type: "double precision", nullable: true),
                    bonus = table.Column<double>(type: "double precision", nullable: true),
                    instructions_version = table.Column<int>(type: "integer", nullable: false),
                    sent_files = table.Column<string[]>(type: "text[]", nullable: false),
                    skipped_files = table.Column<string[]>(type: "text[]", nullable: false),
                    raw_output = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    discarded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_gradings", x => x.id);
                    table.ForeignKey(
                        name: "fk_gradings_submission_versions_version_id",
                        column: x => x.version_id,
                        principalTable: "submission_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "grading_mistakes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    grading_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    file = table.Column<string>(type: "text", nullable: true),
                    start_line = table.Column<int>(type: "integer", nullable: true),
                    end_line = table.Column<int>(type: "integer", nullable: true),
                    severity = table.Column<string>(type: "text", nullable: false),
                    category = table.Column<string>(type: "text", nullable: false),
                    message = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_grading_mistakes", x => x.id);
                    table.ForeignKey(
                        name: "fk_grading_mistakes_gradings_grading_id",
                        column: x => x.grading_id,
                        principalTable: "gradings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_grading_mistakes_grading_id",
                table: "grading_mistakes",
                column: "grading_id");

            migrationBuilder.CreateIndex(
                name: "ix_gradings_status_created_at",
                table: "gradings",
                columns: new[] { "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_gradings_version_id_created_at",
                table: "gradings",
                columns: new[] { "version_id", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "grading_mistakes");

            migrationBuilder.DropTable(
                name: "gradings");
        }
    }
}
