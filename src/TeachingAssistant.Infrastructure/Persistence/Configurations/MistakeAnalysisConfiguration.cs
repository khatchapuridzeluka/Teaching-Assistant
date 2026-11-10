using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Infrastructure.Persistence.Configurations;

public class MistakeAnalysisConfiguration : IEntityTypeConfiguration<MistakeAnalysis>
{
    public void Configure(EntityTypeBuilder<MistakeAnalysis> builder)
    {
        builder.ToTable("mistake_analyses");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.HasIndex(a => new { a.CourseId, a.AssignmentId, a.CreatedAt });

        builder.HasMany(a => a.Clusters)
            .WithOne()
            .HasForeignKey(c => c.AnalysisId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(a => a.Clusters).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public class MistakeClusterConfiguration : IEntityTypeConfiguration<MistakeCluster>
{
    public void Configure(EntityTypeBuilder<MistakeCluster> builder)
    {
        builder.ToTable("mistake_clusters");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.HasIndex(c => c.AnalysisId);

        builder.HasMany(c => c.Members)
            .WithOne()
            .HasForeignKey(m => m.ClusterId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(c => c.Members).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

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
