using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Infrastructure.Persistence.Configurations;

public class ReportStrengthConfiguration : IEntityTypeConfiguration<ReportStrength>
{
    public void Configure(EntityTypeBuilder<ReportStrength> builder)
    {
        builder.ToTable("report_strengths");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.Category).HasConversion<string>();
        builder.Property(s => s.Decision).HasConversion<string>();

        builder.HasOne<GradingStrength>()
            .WithMany()
            .HasForeignKey(s => s.SourceStrengthId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(s => s.ReportId);
    }
}
