using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeachingAssistant.Domain.Ingestion;

namespace TeachingAssistant.Infrastructure.Persistence.Configurations;

public class SubmissionConfiguration : IEntityTypeConfiguration<Submission>
{
    public void Configure(EntityTypeBuilder<Submission> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.Source).HasConversion<string>();

        builder.HasIndex(s => new { s.Source, s.ExternalSubmissionId }).IsUnique();
        builder.HasIndex(s => s.AssignmentId);

        builder.HasMany(s => s.Versions)
            .WithOne()
            .HasForeignKey(v => v.SubmissionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(s => s.Versions).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(s => s.CourseId);
        builder.HasIndex(s => s.StudentId);

        builder.HasMany(s => s.GradeEvents)
            .WithOne()
            .HasForeignKey(e => e.SubmissionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(s => s.GradeEvents).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(s => s.LatestVersion);
        builder.Ignore(s => s.NextVersionNumber);
        builder.Ignore(s => s.Grade);
        builder.Ignore(s => s.IsTurnedIn);
    }
}
