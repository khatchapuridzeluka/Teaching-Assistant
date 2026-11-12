using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeachingAssistant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgentChats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "agent_conversations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    message_count = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_activity_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_agent_conversations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "agent_runs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    conversation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    waiting_job_id = table.Column<Guid>(type: "uuid", nullable: true),
                    steps = table.Column<int>(type: "integer", nullable: false),
                    max_steps = table.Column<int>(type: "integer", nullable: false),
                    reply = table.Column<string>(type: "text", nullable: true),
                    error = table.Column<string>(type: "text", nullable: true),
                    page = table.Column<string>(type: "text", nullable: true),
                    course_id = table.Column<string>(type: "text", nullable: true),
                    assignment_id = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    finished_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_agent_runs", x => x.id);
                    table.ForeignKey(
                        name: "fk_agent_runs_agent_conversations_conversation_id",
                        column: x => x.conversation_id,
                        principalTable: "agent_conversations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_agent_runs_jobs_waiting_job_id",
                        column: x => x.waiting_job_id,
                        principalTable: "jobs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "agent_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    conversation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequence = table.Column<int>(type: "integer", nullable: false),
                    role = table.Column<string>(type: "text", nullable: false),
                    text = table.Column<string>(type: "text", nullable: false),
                    tool_calls = table.Column<string>(type: "jsonb", nullable: true),
                    tool_call_id = table.Column<string>(type: "text", nullable: true),
                    tool_name = table.Column<string>(type: "text", nullable: true),
                    job_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_agent_messages", x => x.id);
                    table.ForeignKey(
                        name: "fk_agent_messages_agent_conversations_conversation_id",
                        column: x => x.conversation_id,
                        principalTable: "agent_conversations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_agent_messages_agent_runs_run_id",
                        column: x => x.run_id,
                        principalTable: "agent_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_agent_messages_jobs_job_id",
                        column: x => x.job_id,
                        principalTable: "jobs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_agent_conversations_last_activity_at",
                table: "agent_conversations",
                column: "last_activity_at");

            migrationBuilder.CreateIndex(
                name: "ix_agent_messages_conversation_id_sequence",
                table: "agent_messages",
                columns: new[] { "conversation_id", "sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_agent_messages_job_id",
                table: "agent_messages",
                column: "job_id");

            migrationBuilder.CreateIndex(
                name: "ix_agent_messages_run_id",
                table: "agent_messages",
                column: "run_id");

            migrationBuilder.CreateIndex(
                name: "ix_agent_runs_conversation_id_created_at",
                table: "agent_runs",
                columns: new[] { "conversation_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_agent_runs_status",
                table: "agent_runs",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_agent_runs_waiting_job_id",
                table: "agent_runs",
                column: "waiting_job_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agent_messages");

            migrationBuilder.DropTable(
                name: "agent_runs");

            migrationBuilder.DropTable(
                name: "agent_conversations");
        }
    }
}
