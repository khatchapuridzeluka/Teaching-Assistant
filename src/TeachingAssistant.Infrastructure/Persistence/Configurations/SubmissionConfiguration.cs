using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeachingAssistant.Domain.Ingestion;

namespace TeachingAssistant.Infrastructure.Persistence.Configurations;

public class SubmissionConfiguration : IEntityTypeConfiguration<Submission>
{
    public void Configure(EntityTypeBuilder<Submission> builder)
    {
        builder.HasKey(s => s.Id);

        // IDs are created in Domain (Guid v7), so EF must treat a new entity with an ID as new, not existing.
        builder.Property(s => s.Id).ValueGeneratedNever();

        // Enums as text: readable in SQL and safe if the enum order changes.
        builder.Property(s => s.Source).HasConversion<string>();

        // One record per source submission, so a re-fetch finds it instead of duplicating it.
        builder.HasIndex(s => new { s.Source, s.ExternalSubmissionId }).IsUnique();
        builder.HasIndex(s => s.AssignmentId);

        builder.HasMany(s => s.Versions)
            .WithOne()
            .HasForeignKey(v => v.SubmissionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(s => s.Versions).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(s => s.LatestVersion);
        builder.Ignore(s => s.NextVersionNumber);
    }
}
