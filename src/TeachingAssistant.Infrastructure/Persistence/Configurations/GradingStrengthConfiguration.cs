using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Infrastructure.Persistence.Configurations;

public class GradingStrengthConfiguration : IEntityTypeConfiguration<GradingStrength>
{
    public void Configure(EntityTypeBuilder<GradingStrength> builder)
    {
        builder.ToTable("grading_strengths");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.Category).HasConversion<string>();
        builder.HasIndex(s => s.GradingId);
    }
}
