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
