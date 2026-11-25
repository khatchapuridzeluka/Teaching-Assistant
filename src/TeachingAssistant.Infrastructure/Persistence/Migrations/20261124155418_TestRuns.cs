using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeachingAssistant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TestRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "test_runs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    test_set_id = table.Column<Guid>(type: "uuid", nullable: false),
                    test_set_version = table.Column<int>(type: "integer", nullable: false),
                    version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    course_id = table.Column<string>(type: "text", nullable: false),
                    assignment_id = table.Column<string>(type: "text", nullable: false),
                    student_id = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    @interface = table.Column<string>(name: "interface", type: "text", nullable: true),
                    adapter = table.Column<string>(type: "text", nullable: true),
                    adapter_model = table.Column<string>(type: "text", nullable: true),
                    results = table.Column<string>(type: "jsonb", nullable: false),
                    output = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_test_runs", x => x.id);
                    table.ForeignKey(
                        name: "fk_test_runs_test_sets_test_set_id",
                        column: x => x.test_set_id,
                        principalTable: "test_sets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_test_runs_test_set_id_version_id_created_at",
                table: "test_runs",
                columns: new[] { "test_set_id", "version_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_test_runs_version_id_created_at",
                table: "test_runs",
                columns: new[] { "version_id", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "test_runs");
        }
    }
}
