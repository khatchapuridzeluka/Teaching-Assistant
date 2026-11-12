using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeachingAssistant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgentRunWaits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_agent_runs_jobs_waiting_job_id",
                table: "agent_runs");

            migrationBuilder.RenameColumn(
                name: "waiting_job_id",
                table: "agent_runs",
                newName: "job_id");

            migrationBuilder.RenameIndex(
                name: "ix_agent_runs_waiting_job_id",
                table: "agent_runs",
                newName: "ix_agent_runs_job_id");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "wait_until",
                table: "agent_runs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid[]>(
                name: "waiting_job_ids",
                table: "agent_runs",
                type: "uuid[]",
                nullable: false,
                defaultValue: new Guid[0]);

            migrationBuilder.CreateIndex(
                name: "ix_agent_runs_waiting_job_ids",
                table: "agent_runs",
                column: "waiting_job_ids")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.AddForeignKey(
                name: "fk_agent_runs_jobs_job_id",
                table: "agent_runs",
                column: "job_id",
                principalTable: "jobs",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_agent_runs_jobs_job_id",
                table: "agent_runs");

            migrationBuilder.DropIndex(
                name: "ix_agent_runs_waiting_job_ids",
                table: "agent_runs");

            migrationBuilder.DropColumn(
                name: "wait_until",
                table: "agent_runs");

            migrationBuilder.DropColumn(
                name: "waiting_job_ids",
                table: "agent_runs");

            migrationBuilder.RenameColumn(
                name: "job_id",
                table: "agent_runs",
                newName: "waiting_job_id");

            migrationBuilder.RenameIndex(
                name: "ix_agent_runs_job_id",
                table: "agent_runs",
                newName: "ix_agent_runs_waiting_job_id");

            migrationBuilder.AddForeignKey(
                name: "fk_agent_runs_jobs_waiting_job_id",
                table: "agent_runs",
                column: "waiting_job_id",
                principalTable: "jobs",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
