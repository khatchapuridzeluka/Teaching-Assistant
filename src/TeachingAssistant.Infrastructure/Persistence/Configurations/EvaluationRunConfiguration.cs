using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Infrastructure.Persistence.Configurations;

public class EvaluationRunConfiguration : IEntityTypeConfiguration<EvaluationRun>
{
    public void Configure(EntityTypeBuilder<EvaluationRun> builder)
    {
        builder.ToTable("evaluation_runs");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.HasIndex(r => new { r.CourseId, r.CreatedAt });
    }
}

public class EvaluationRunResultConfiguration : IEntityTypeConfiguration<EvaluationRunResult>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public void Configure(EntityTypeBuilder<EvaluationRunResult> builder)
    {
        builder.ToTable("evaluation_run_results");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.Status).HasConversion<string>();
        builder.Property(r => r.BuildStatus).HasConversion<string>();
        builder.Property(r => r.Error).HasMaxLength(EvaluationRunResult.MaxErrorLength);

        // The mistakes are only read back whole, so they live in one JSON column next to the result.
        builder.Property(r => r.Mistakes)
            .HasColumnType("jsonb")
            .HasConversion(
                mistakes => JsonSerializer.Serialize(mistakes, Json),
                json => JsonSerializer.Deserialize<List<RunMistake>>(json, Json) ?? new List<RunMistake>(),
                new ValueComparer<List<RunMistake>>(
                    (a, b) => a != null && b != null && a.SequenceEqual(b),
                    list => list.Aggregate(0, (hash, m) => HashCode.Combine(hash, m.GetHashCode())),
                    list => list.ToList()));

        builder.HasOne<EvaluationRun>()
            .WithMany()
            .HasForeignKey(r => r.RunId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(r => new { r.ReportId, r.Model });
        builder.HasIndex(r => r.Status);
    }
}
