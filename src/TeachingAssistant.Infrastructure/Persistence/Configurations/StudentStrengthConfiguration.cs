using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeachingAssistant.Domain.Grading;
using TeachingAssistant.Domain.Profiling;

namespace TeachingAssistant.Infrastructure.Persistence.Configurations;

public class StudentStrengthConfiguration : IEntityTypeConfiguration<StudentStrength>
{
    public void Configure(EntityTypeBuilder<StudentStrength> builder)
    {
        builder.ToTable("student_strengths");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.Category).HasConversion<string>();
        builder.Property(s => s.Decision).HasConversion<string>();

        builder.HasOne<GradingReport>()
            .WithMany()
            .HasForeignKey(s => s.ReportId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ReportStrength>()
            .WithMany()
            .HasForeignKey(s => s.ReportStrengthId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => new { s.CourseId, s.StudentId, s.ApprovedAt });
        builder.HasIndex(s => new { s.StudentId, s.Category });
        builder.HasIndex(s => new { s.CourseId, s.AssignmentId });
        builder.HasIndex(s => s.ReportId);
        builder.HasIndex(s => s.ReportStrengthId).IsUnique();
    }
}
