using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeachingAssistant.Domain.Ingestion;

namespace TeachingAssistant.Infrastructure.Persistence.Configurations;

public class SubmissionVersionConfiguration : IEntityTypeConfiguration<SubmissionVersion>
{
    public void Configure(EntityTypeBuilder<SubmissionVersion> builder)
    {
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).ValueGeneratedNever();
        builder.Property(v => v.Status).HasConversion<string>();

        builder.HasIndex(v => new { v.SubmissionId, v.Number }).IsUnique();

        builder.HasMany(v => v.Contents)
            .WithOne()
            .HasForeignKey(c => c.VersionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(v => v.Contents).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
