using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeachingAssistant.Domain.Ingestion;

namespace TeachingAssistant.Infrastructure.Persistence.Configurations;

public class InstructionsVersionConfiguration : IEntityTypeConfiguration<InstructionsVersion>
{
    public void Configure(EntityTypeBuilder<InstructionsVersion> builder)
    {
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).ValueGeneratedNever();
        builder.HasIndex(v => new { v.InstructionsId, v.Number }).IsUnique();

        builder.HasMany(v => v.Contents)
            .WithOne()
            .HasForeignKey(c => c.VersionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(v => v.Contents).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
