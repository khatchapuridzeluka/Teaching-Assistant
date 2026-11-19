using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeachingAssistant.Domain.Grading;
using TeachingAssistant.Domain.Ingestion;

namespace TeachingAssistant.Infrastructure.Persistence.Configurations;

public class AiGradingConfiguration : IEntityTypeConfiguration<AiGrading>
{
    public void Configure(EntityTypeBuilder<AiGrading> builder)
    {
        builder.ToTable("gradings");
        builder.HasKey(g => g.Id);
        builder.Property(g => g.Id).ValueGeneratedNever();
        builder.Property(g => g.Status).HasConversion<string>();

        builder.Property(g => g.RawOutput).HasColumnType("text");
        builder.Property(g => g.BuildStatus).HasConversion<string>();
        builder.Property(g => g.BuildOutput).HasColumnType("text");
        builder.Property(g => g.StyleOutput).HasColumnType("text");
        builder.Property(g => g.PmdOutput).HasColumnType("text");
        builder.Property(g => g.DiscardReason).HasConversion<string>();
        builder.Property(g => g.DiscardNote).HasMaxLength(AiGrading.MaxDiscardNoteLength);

        builder.HasOne<SubmissionVersion>()
            .WithMany()
            .HasForeignKey(g => g.VersionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(g => new { g.VersionId, g.CreatedAt });
        builder.HasIndex(g => new { g.Status, g.CreatedAt });

        builder.HasMany(g => g.Mistakes)
            .WithOne()
            .HasForeignKey(m => m.GradingId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(g => g.Mistakes).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(g => g.Strengths)
            .WithOne()
            .HasForeignKey(s => s.GradingId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(g => g.Strengths).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public class GradingStrengthConfiguration : IEntityTypeConfiguration<GradingStrength>
{
    public void Configure(EntityTypeBuilder<GradingStrength> builder)
    {
        builder.ToTable("grading_strengths");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.Category).HasConversion<string>();
        builder.HasIndex(s => s.GradingId);
    }
}

public class GradingMistakeConfiguration : IEntityTypeConfiguration<GradingMistake>
{
    public void Configure(EntityTypeBuilder<GradingMistake> builder)
    {
        builder.ToTable("grading_mistakes");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.Property(m => m.Severity).HasConversion<string>();
        builder.Property(m => m.Category).HasConversion<string>();
        builder.HasIndex(m => m.GradingId);
    }
}
