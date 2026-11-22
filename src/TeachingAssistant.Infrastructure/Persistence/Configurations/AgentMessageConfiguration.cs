using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeachingAssistant.Domain.Agents;
using TeachingAssistant.Domain.Jobs;

namespace TeachingAssistant.Infrastructure.Persistence.Configurations;

public class AgentMessageConfiguration : IEntityTypeConfiguration<AgentMessage>
{
    public void Configure(EntityTypeBuilder<AgentMessage> builder)
    {
        builder.ToTable("agent_messages");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.Property(m => m.Role).HasConversion<string>();
        builder.Property(m => m.Text).HasColumnType("text");
        builder.Property(m => m.ToolCalls).HasColumnType("jsonb");
        builder.Property(m => m.Links).HasColumnType("jsonb");

        builder.HasOne<AgentConversation>()
            .WithMany()
            .HasForeignKey(m => m.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<AgentRun>()
            .WithMany()
            .HasForeignKey(m => m.RunId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Job>()
            .WithMany()
            .HasForeignKey(m => m.JobId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(m => new { m.ConversationId, m.Sequence }).IsUnique();
        builder.HasIndex(m => m.RunId);
        builder.HasIndex(m => m.JobId);
    }
}
