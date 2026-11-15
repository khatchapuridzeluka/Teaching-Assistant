using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Infrastructure.Persistence.Configurations;

public class PolicyRuleConfiguration : IEntityTypeConfiguration<PolicyRule>
{
    public void Configure(EntityTypeBuilder<PolicyRule> builder)
    {
        builder.ToTable("policy_rules");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.Effect).HasConversion<string>().HasMaxLength(20);
        builder.Property(r => r.Title).HasMaxLength(PolicyRule.MaxTitleLength);
        builder.Property(r => r.Covers).HasMaxLength(PolicyRule.MaxCoversLength);
        builder.Property(r => r.PointsPer).HasMaxLength(PolicyRule.MaxPointsPerLength);
        builder.Property(r => r.Detail).HasMaxLength(PolicyRule.MaxDetailLength);
        builder.HasIndex(r => new { r.IsDeleted, r.Position });
    }
}
