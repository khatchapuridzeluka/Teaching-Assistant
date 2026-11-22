using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeachingAssistant.Domain.Agents;
using TeachingAssistant.Domain.Jobs;

namespace TeachingAssistant.Infrastructure.Persistence.Configurations;

public class AgentRunConfiguration : IEntityTypeConfiguration<AgentRun>
{
    public void Configure(EntityTypeBuilder<AgentRun> builder)
    {
        builder.ToTable("agent_runs");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.Status).HasConversion<string>();
        builder.Property(r => r.Reply).HasColumnType("text");
        builder.Property(r => r.Error).HasColumnType("text");

        builder.HasOne<AgentConversation>()
            .WithMany()
            .HasForeignKey(r => r.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Job>()
            .WithMany()
            .HasForeignKey(r => r.JobId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(r => new { r.ConversationId, r.CreatedAt });
        builder.HasIndex(r => r.WaitingJobIds).HasMethod("gin");
        builder.HasIndex(r => r.Status);
        builder.HasIndex(r => r.JobId);

        builder.Ignore(r => r.IsFinished);
    }
}
