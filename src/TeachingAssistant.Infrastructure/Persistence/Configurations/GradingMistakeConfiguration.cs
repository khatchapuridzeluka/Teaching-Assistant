using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Infrastructure.Persistence.Configurations;

public class GradingMistakeConfiguration : IEntityTypeConfiguration<GradingMistake>
{
    public void Configure(EntityTypeBuilder<GradingMistake> builder)
    {
        builder.ToTable("grading_mistakes");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.Property(m => m.Severity).HasConversion<string>();
        builder.Property(m => m.Category).HasConversion<string>();
        builder.HasIndex(m => m.GradingId);
    }
}
