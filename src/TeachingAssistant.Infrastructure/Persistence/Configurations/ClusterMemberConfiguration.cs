using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Infrastructure.Persistence.Configurations;

public class ClusterMemberConfiguration : IEntityTypeConfiguration<ClusterMember>
{
    public void Configure(EntityTypeBuilder<ClusterMember> builder)
    {
        builder.ToTable("mistake_cluster_members");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.Property(m => m.Severity).HasConversion<string>();
        builder.Property(m => m.Category).HasConversion<string>();
        builder.HasIndex(m => m.ClusterId);
        builder.HasIndex(m => m.StudentId);
    }
}
