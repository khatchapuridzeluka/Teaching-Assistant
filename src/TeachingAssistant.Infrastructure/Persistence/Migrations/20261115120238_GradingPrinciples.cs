using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeachingAssistant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GradingPrinciples : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "grading_principles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    detail = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_grading_principles", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_grading_principles_is_deleted_position",
                table: "grading_principles",
                columns: new[] { "is_deleted", "position" });

            // The six principles the grader used while they were written in the code, word for word, so grading does not
            // change when they move into the database.
            var seeded = new DateTimeOffset(2026, 11, 15, 0, 0, 0, TimeSpan.Zero);
            migrationBuilder.InsertData(
                table: "grading_principles",
                columns: new[] { "id", "position", "title", "detail", "is_deleted", "created_at", "updated_at" },
                values: new object[,]
                {
                { new Guid("0196a3c0-0000-7000-8000-000000000001"), 1, "Follow the points in the instructions",
                    "The assignment's instructions (the attached instruction files and the task statement) define the tasks and how\n  many points each is worth. Follow that breakdown exactly. If the instructions give no points, split the\n  assignment's maximum points across the tasks sensibly and say that you did.", false, seeded, seeded },
                { new Guid("0196a3c0-0000-7000-8000-000000000002"), 2, "Judge by reading the code",
                    "You only see the source code; it has not been compiled or run. Judge behaviour by reading the code carefully.\n  When the instructions give an expected output, trace the code to check whether it would produce the same\n  values and behaviour, and say \"likely\" when you cannot be sure. Small differences in the printed text itself\n  do not count (see the grading policy below).", false, seeded, seeded },
                { new Guid("0196a3c0-0000-7000-8000-000000000003"), 3, "Deduct only for real problems",
                    "Deduct points only for real problems measured against the instructions: missing or wrong functionality, rules\n  the instructions state (for example that fields must be private), and clear design mistakes for this course\n  level. Do not deduct for style choices the instructions do not mention, and do not invent requirements.", false, seeded, seeded },
                { new Guid("0196a3c0-0000-7000-8000-000000000004"), 4, "A different valid solution earns full points",
                    "A requirement met in a different but valid way (other variable names, extra helper methods) earns full points,\n  unless the instructions fix the exact form.", false, seeded, seeded },
                { new Guid("0196a3c0-0000-7000-8000-000000000005"), 5, "A bonus only for a finished bonus task",
                    "Award a bonus only when its task is actually done, and show it separately from the base total.", false, seeded, seeded },
                { new Guid("0196a3c0-0000-7000-8000-000000000006"), 6, "Don't deduct for files it could not see",
                    "Some submitted files may be listed as not included; do not deduct points for content you could not see, but\n  mention it.", false, seeded, seeded }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "grading_principles");
        }
    }
}
