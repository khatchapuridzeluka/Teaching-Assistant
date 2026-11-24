using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeachingAssistant.Domain.Testing;

namespace TeachingAssistant.Infrastructure.Persistence.Configurations;

public class TestSetConfiguration : IEntityTypeConfiguration<TestSet>
{
    public void Configure(EntityTypeBuilder<TestSet> builder)
    {
        builder.ToTable("test_sets");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.Property(t => t.Style).HasConversion<string>();
        builder.Property(t => t.Status).HasConversion<string>();
        builder.Property(t => t.Request).HasColumnType("jsonb");
        builder.Property(t => t.Content).HasColumnType("jsonb");
        builder.HasIndex(t => new { t.AssignmentId, t.Version }).IsUnique();
    }
}
