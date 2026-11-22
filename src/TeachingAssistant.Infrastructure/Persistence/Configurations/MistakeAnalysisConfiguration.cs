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
