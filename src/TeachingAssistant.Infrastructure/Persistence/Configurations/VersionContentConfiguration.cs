using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeachingAssistant.Domain.Ingestion;

namespace TeachingAssistant.Infrastructure.Persistence.Configurations;

public class VersionContentConfiguration : IEntityTypeConfiguration<VersionContent>
{
    public void Configure(EntityTypeBuilder<VersionContent> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Origin).HasConversion<string>();
        builder.Property(c => c.Kind).HasConversion<string>();
        builder.Property(c => c.Status).HasConversion<string>();
    }
}
