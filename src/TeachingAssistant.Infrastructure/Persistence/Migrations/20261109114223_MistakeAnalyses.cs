using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeachingAssistant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MistakeAnalyses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "mistake_analyses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    course_id = table.Column<string>(type: "text", nullable: false),
                    assignment_id = table.Column<string>(type: "text", nullable: false),
                    model = table.Column<string>(type: "text", nullable: false),
                    students = table.Column<int>(type: "integer", nullable: false),
                    mistakes = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mistake_analyses", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "mistake_clusters",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    analysis_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    explanation = table.Column<string>(type: "text", nullable: false),
                    ungrouped = table.Column<bool>(type: "boolean", nullable: false),
                    approved_explanation = table.Column<string>(type: "text", nullable: true),
                    approved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mistake_clusters", x => x.id);
                    table.ForeignKey(
                        name: "fk_mistake_clusters_mistake_analyses_analysis_id",
                        column: x => x.analysis_id,
                        principalTable: "mistake_analyses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "mistake_cluster_members",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cluster_id = table.Column<Guid>(type: "uuid", nullable: false),
                    grading_mistake_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<string>(type: "text", nullable: false),
                    student_name = table.Column<string>(type: "text", nullable: false),
                    submission_id = table.Column<string>(type: "text", nullable: false),
                    severity = table.Column<string>(type: "text", nullable: false),
                    category = table.Column<string>(type: "text", nullable: false),
                    points = table.Column<double>(type: "double precision", nullable: true),
                    message = table.Column<string>(type: "text", nullable: false),
                    file = table.Column<string>(type: "text", nullable: true),
                    start_line = table.Column<int>(type: "integer", nullable: true),
                    end_line = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mistake_cluster_members", x => x.id);
                    table.ForeignKey(
                        name: "fk_mistake_cluster_members_mistake_clusters_cluster_id",
                        column: x => x.cluster_id,
                        principalTable: "mistake_clusters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_mistake_analyses_course_id_assignment_id_created_at",
                table: "mistake_analyses",
                columns: new[] { "course_id", "assignment_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_mistake_cluster_members_cluster_id",
                table: "mistake_cluster_members",
                column: "cluster_id");

            migrationBuilder.CreateIndex(
                name: "ix_mistake_cluster_members_student_id",
                table: "mistake_cluster_members",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "ix_mistake_clusters_analysis_id",
                table: "mistake_clusters",
                column: "analysis_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "mistake_cluster_members");

            migrationBuilder.DropTable(
                name: "mistake_clusters");

            migrationBuilder.DropTable(
                name: "mistake_analyses");
        }
    }
}
