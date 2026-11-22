using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeachingAssistant.Domain.Ingestion;

namespace TeachingAssistant.Infrastructure.Persistence.Configurations;

public class InstructionsContentConfiguration : IEntityTypeConfiguration<InstructionsContent>
{
    public void Configure(EntityTypeBuilder<InstructionsContent> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Origin).HasConversion<string>();
        builder.Property(c => c.Status).HasConversion<string>();
    }
}
