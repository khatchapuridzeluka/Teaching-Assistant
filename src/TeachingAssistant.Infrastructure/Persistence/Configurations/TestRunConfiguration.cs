using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeachingAssistant.Domain.Testing;

namespace TeachingAssistant.Infrastructure.Persistence.Configurations;

public class TestRunConfiguration : IEntityTypeConfiguration<TestRun>
{
    public void Configure(EntityTypeBuilder<TestRun> builder)
    {
        builder.ToTable("test_runs");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.Status).HasConversion<string>();
        builder.Property(r => r.Results).HasColumnType("jsonb");
        builder.HasIndex(r => new { r.TestSetId, r.VersionId, r.CreatedAt });
        builder.HasIndex(r => new { r.VersionId, r.CreatedAt });
        builder.HasOne<TestSet>().WithMany().HasForeignKey(r => r.TestSetId).OnDelete(DeleteBehavior.Cascade);
    }
}
