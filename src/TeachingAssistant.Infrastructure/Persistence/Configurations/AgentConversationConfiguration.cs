using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeachingAssistant.Domain.Agents;
using TeachingAssistant.Domain.Jobs;

namespace TeachingAssistant.Infrastructure.Persistence.Configurations;

public class AgentConversationConfiguration : IEntityTypeConfiguration<AgentConversation>
{
    public void Configure(EntityTypeBuilder<AgentConversation> builder)
    {
        builder.ToTable("agent_conversations");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.HasIndex(c => c.LastActivityAt);
    }
}

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
