using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeachingAssistant.Domain.Profiling;

namespace TeachingAssistant.Infrastructure.Persistence.Configurations;

public class StudentConfiguration : IEntityTypeConfiguration<Student>
{
    public void Configure(EntityTypeBuilder<Student> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.Source).HasConversion<string>();

        // One record per person in the source, so a re-sync finds them instead of duplicating them.
        builder.HasIndex(s => new { s.Source, s.ExternalUserId }).IsUnique();

        builder.HasMany(s => s.Enrollments)
            .WithOne()
            .HasForeignKey(e => e.StudentId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(s => s.Enrollments).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public class EnrollmentConfiguration : IEntityTypeConfiguration<Enrollment>
{
    public void Configure(EntityTypeBuilder<Enrollment> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        // A student is in a course once.
        builder.HasIndex(e => new { e.StudentId, e.CourseId }).IsUnique();
        builder.HasIndex(e => e.CourseId);
    }
}
