using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Infrastructure.Persistence.Configurations;

public class ReportMistakeConfiguration : IEntityTypeConfiguration<ReportMistake>
{
    public void Configure(EntityTypeBuilder<ReportMistake> builder)
    {
        builder.ToTable("report_mistakes");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.Property(m => m.Severity).HasConversion<string>();
        builder.Property(m => m.Category).HasConversion<string>();
        builder.Property(m => m.Decision).HasConversion<string>();

        builder.HasOne<GradingMistake>()
            .WithMany()
            .HasForeignKey(m => m.SourceMistakeId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(m => m.ReportId);
    }
}
