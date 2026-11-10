using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeachingAssistant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StudentAnalysisKinds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_student_analyses_course_id_student_id_created_at",
                table: "student_analyses");

            migrationBuilder.DropColumn(
                name: "strengths",
                table: "student_analyses");

            migrationBuilder.RenameColumn(
                name: "mistakes",
                table: "student_analyses",
                newName: "records");

            migrationBuilder.AddColumn<string>(
                name: "kind",
                table: "student_analyses",
                type: "text",
                nullable: false,
                defaultValue: "Mistakes");

            migrationBuilder.CreateTable(
                name: "student_strength_groups",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    analysis_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    explanation = table.Column<string>(type: "text", nullable: false),
                    ungrouped = table.Column<bool>(type: "boolean", nullable: false),
                    strength_ids = table.Column<Guid[]>(type: "uuid[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_student_strength_groups", x => x.id);
                    table.ForeignKey(
                        name: "fk_student_strength_groups_student_analyses_analysis_id",
                        column: x => x.analysis_id,
                        principalTable: "student_analyses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_student_analyses_course_id_student_id_kind_created_at",
                table: "student_analyses",
                columns: new[] { "course_id", "student_id", "kind", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_student_strength_groups_analysis_id",
                table: "student_strength_groups",
                column: "analysis_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "student_strength_groups");

            migrationBuilder.DropIndex(
                name: "ix_student_analyses_course_id_student_id_kind_created_at",
                table: "student_analyses");

            migrationBuilder.DropColumn(
                name: "kind",
                table: "student_analyses");

            migrationBuilder.RenameColumn(
                name: "records",
                table: "student_analyses",
                newName: "mistakes");

            migrationBuilder.AddColumn<int>(
                name: "strengths",
                table: "student_analyses",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "ix_student_analyses_course_id_student_id_created_at",
                table: "student_analyses",
                columns: new[] { "course_id", "student_id", "created_at" });
        }
    }
}
