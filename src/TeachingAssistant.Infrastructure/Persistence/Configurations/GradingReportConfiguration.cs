using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Infrastructure.Persistence.Configurations;

public class GradingReportConfiguration : IEntityTypeConfiguration<GradingReport>
{
    public void Configure(EntityTypeBuilder<GradingReport> builder)
    {
        builder.ToTable("grading_reports");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.Status).HasConversion<string>();

        builder.HasOne<AiGrading>()
            .WithMany()
            .HasForeignKey(r => r.GradingId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(r => r.GradingId).IsUnique();
        builder.HasIndex(r => r.VersionId);
        builder.HasIndex(r => r.Status);
        builder.HasIndex(r => new { r.CourseId, r.StudentId, r.ApprovedAt });
        builder.HasIndex(r => new { r.CourseId, r.AssignmentId });

        builder.HasMany(r => r.Mistakes)
            .WithOne()
            .HasForeignKey(m => m.ReportId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(r => r.Mistakes).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(r => r.Strengths)
            .WithOne()
            .HasForeignKey(s => s.ReportId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(r => r.Strengths).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
