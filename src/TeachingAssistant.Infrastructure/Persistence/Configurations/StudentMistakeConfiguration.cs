using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeachingAssistant.Domain.Grading;
using TeachingAssistant.Domain.Profiling;

namespace TeachingAssistant.Infrastructure.Persistence.Configurations;

public class StudentMistakeConfiguration : IEntityTypeConfiguration<StudentMistake>
{
    public void Configure(EntityTypeBuilder<StudentMistake> builder)
    {
        builder.ToTable("student_mistakes");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.Property(m => m.Severity).HasConversion<string>();
        builder.Property(m => m.Category).HasConversion<string>();
        builder.Property(m => m.Decision).HasConversion<string>();

        builder.HasOne<GradingReport>()
            .WithMany()
            .HasForeignKey(m => m.ReportId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ReportMistake>()
            .WithMany()
            .HasForeignKey(m => m.ReportMistakeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(m => new { m.CourseId, m.StudentId, m.ApprovedAt });
        builder.HasIndex(m => new { m.StudentId, m.Category });
        builder.HasIndex(m => new { m.CourseId, m.AssignmentId });
        builder.HasIndex(m => m.ReportId);
        builder.HasIndex(m => m.ReportMistakeId).IsUnique();
    }
}
