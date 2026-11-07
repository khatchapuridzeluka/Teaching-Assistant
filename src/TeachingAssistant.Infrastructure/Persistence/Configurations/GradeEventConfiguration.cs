using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Infrastructure.Persistence.Configurations;

public class GradeEventConfiguration : IEntityTypeConfiguration<GradeEvent>
{
    public void Configure(EntityTypeBuilder<GradeEvent> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Kind).HasConversion<string>();
        builder.HasIndex(e => e.SubmissionId);
    }
}
