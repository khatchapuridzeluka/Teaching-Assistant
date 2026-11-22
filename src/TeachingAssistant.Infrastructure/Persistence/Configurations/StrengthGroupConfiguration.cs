using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeachingAssistant.Domain.Profiling;

namespace TeachingAssistant.Infrastructure.Persistence.Configurations;

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
