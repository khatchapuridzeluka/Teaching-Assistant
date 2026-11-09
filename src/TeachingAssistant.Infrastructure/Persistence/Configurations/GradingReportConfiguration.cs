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
    }
}

public class ReportMistakeConfiguration : IEntityTypeConfiguration<ReportMistake>
{
    public void Configure(EntityTypeBuilder<ReportMistake> builder)
    {
        builder.ToTable("report_mistakes");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.Property(m => m.Severity).HasConversion<string>();
        builder.Property(m => m.Category).HasConversion<string>();
        builder.Property(m => m.Decision).HasConversion<string>();

        builder.HasOne<GradingMistake>()
            .WithMany()
            .HasForeignKey(m => m.SourceMistakeId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(m => m.ReportId);
    }
}
