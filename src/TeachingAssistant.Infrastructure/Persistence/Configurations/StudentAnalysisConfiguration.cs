using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeachingAssistant.Domain.Profiling;

namespace TeachingAssistant.Infrastructure.Persistence.Configurations;

public class StudentAnalysisConfiguration : IEntityTypeConfiguration<StudentAnalysis>
{
    public void Configure(EntityTypeBuilder<StudentAnalysis> builder)
    {
        builder.ToTable("student_analyses");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.Kind).HasConversion<string>();
        builder.Property(a => a.Summary).HasColumnType("text");
        builder.Property(a => a.NextSteps).HasColumnType("text");
        builder.HasIndex(a => new { a.CourseId, a.StudentId, a.Kind, a.CreatedAt });

        builder.HasMany(a => a.Groups)
            .WithOne()
            .HasForeignKey(g => g.AnalysisId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(a => a.Groups).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public class StrengthGroupConfiguration : IEntityTypeConfiguration<StrengthGroup>
{
    public void Configure(EntityTypeBuilder<StrengthGroup> builder)
    {
        builder.ToTable("student_strength_groups");
        builder.HasKey(g => g.Id);
        builder.Property(g => g.Id).ValueGeneratedNever();
        builder.Property(g => g.Explanation).HasColumnType("text");
        builder.HasIndex(g => g.AnalysisId);
    }
}
