using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Infrastructure.Persistence.Configurations;

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
