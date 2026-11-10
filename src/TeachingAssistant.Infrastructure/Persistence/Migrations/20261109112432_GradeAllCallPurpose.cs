using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeachingAssistant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GradeAllCallPurpose : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE model_calls AS c
                SET purpose = 'GradeAll'
                FROM jobs AS j
                WHERE c.purpose = 'Grading'
                  AND j.parent_id IS NOT NULL
                  AND j.type = 'GradeVersion'
                  AND c.version_id = (j.payload ->> 'versionId')::uuid
                  AND c.created_at >= j.started_at
                  AND c.created_at <= COALESCE(j.finished_at, now());
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE model_calls SET purpose = 'Grading' WHERE purpose = 'GradeAll';");
        }
    }
}
