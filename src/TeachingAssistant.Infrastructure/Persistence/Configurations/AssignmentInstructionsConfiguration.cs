using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeachingAssistant.Domain.Ingestion;

namespace TeachingAssistant.Infrastructure.Persistence.Configurations;

public class AssignmentInstructionsConfiguration : IEntityTypeConfiguration<AssignmentInstructions>
{
    public void Configure(EntityTypeBuilder<AssignmentInstructions> builder)
    {
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();
        builder.Property(i => i.Source).HasConversion<string>();

        builder.HasIndex(i => new { i.Source, i.AssignmentId }).IsUnique();

        builder.HasMany(i => i.Versions)
            .WithOne()
            .HasForeignKey(v => v.InstructionsId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(i => i.Versions).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(i => i.LatestVersion);
        builder.Ignore(i => i.NextVersionNumber);
    }
}
