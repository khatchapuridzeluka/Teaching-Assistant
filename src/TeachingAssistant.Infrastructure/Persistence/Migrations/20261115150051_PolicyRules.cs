using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeachingAssistant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PolicyRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "policy_rules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    effect = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    points = table.Column<double>(type: "double precision", nullable: true),
                    points_per = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    covers = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    detail = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_policy_rules", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_policy_rules_is_deleted_position",
                table: "policy_rules",
                columns: new[] { "is_deleted", "position" });

            // The two rules the grader used while they were written in the code, word for word, so grading does not change
            // when they move into the database.
            var seeded = new DateTimeOffset(2026, 11, 15, 0, 0, 0, TimeSpan.Zero);
            migrationBuilder.InsertData(
                table: "policy_rules",
                columns: new[] { "id", "position", "title", "effect", "points", "points_per", "covers", "detail", "is_deleted", "created_at", "updated_at" },
                values: new object[,]
                {
                { new Guid("0196a3c0-0000-7000-8000-000000000101"), 1, "Trivial issues cost nothing", "NoDeduction", null, null,
                    "Wording or spacing of printed text, comments, unused imports and unused variables",
                    "Trivial issues never cost points. The teacher wants students judged on whether their program works and is\n  designed well, not punished for polish. Trivial means: the wording, spacing, punctuation, capitalization or\n  labels of printed text (as long as the printed values are right); comments (missing, too few, or with typos);\n  unused imports; and unused or unnecessary variables. Still list each one in \"mistakes\" with severity Style, so\n  the student sees it as a suggestion, and say in the message that no points were deducted.", false, seeded, seeded },
                { new Guid("0196a3c0-0000-7000-8000-000000000102"), 2, "Changing a name the instructions fix", "Deduction", 3.0, "per name",
                    "A variable or method whose exact name the instructions give, spelled or capitalized differently",
                    "Names fixed by the instructions: when the instructions specify the exact name of a variable or method and the\n  student used a different one, deduct 3 points for each such name (a misspelling or different capitalization\n  counts as different). The teacher relies on these names, for example to test the code. List each one with\n  severity Minor and say in the message that it cost 3 points and in which task. Names the instructions do not\n  fix are the student's choice: a poor name there is a Style suggestion that costs nothing.", false, seeded, seeded }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "policy_rules");
        }
    }
}
