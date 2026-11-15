using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Infrastructure.Persistence.Configurations;

public class GradingPrincipleConfiguration : IEntityTypeConfiguration<GradingPrinciple>
{
    public void Configure(EntityTypeBuilder<GradingPrinciple> builder)
    {
        builder.ToTable("grading_principles");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.Title).HasMaxLength(GradingPrinciple.MaxTitleLength);
        builder.Property(p => p.Detail).HasMaxLength(GradingPrinciple.MaxDetailLength);
        builder.HasIndex(p => new { p.IsDeleted, p.Position });
    }
}
