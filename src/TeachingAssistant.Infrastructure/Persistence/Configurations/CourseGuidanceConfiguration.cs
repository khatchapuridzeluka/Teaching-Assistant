using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Infrastructure.Persistence.Configurations;

public class CourseGuidanceConfiguration : IEntityTypeConfiguration<CourseGuidance>
{
    public void Configure(EntityTypeBuilder<CourseGuidance> builder)
    {
        builder.ToTable("course_guidance");
        builder.HasKey(g => g.CourseId);
        builder.Property(g => g.Text).HasMaxLength(CourseGuidance.MaxTextLength);
    }
}
