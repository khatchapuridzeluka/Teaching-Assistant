using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeachingAssistant.Domain.Jobs;

namespace TeachingAssistant.Infrastructure.Persistence.Configurations;

public class JobConfiguration : IEntityTypeConfiguration<Job>
{
    public void Configure(EntityTypeBuilder<Job> builder)
    {
        builder.ToTable("jobs");
        builder.HasKey(j => j.Id);
        builder.Property(j => j.Id).ValueGeneratedNever();
        builder.Property(j => j.Type).HasConversion<string>();
        builder.Property(j => j.Status).HasConversion<string>();
        builder.Property(j => j.Payload).HasColumnType("jsonb");
        builder.Property(j => j.Result).HasColumnType("jsonb");

        builder.HasOne<Job>()
            .WithMany()
            .HasForeignKey(j => j.ParentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(j => new { j.Status, j.RunAfter });
        builder.HasIndex(j => j.ParentId);
        builder.HasIndex(j => j.CourseId);
        builder.HasIndex(j => j.AssignmentId);
        builder.HasIndex(j => j.DedupKey)
            .IsUnique()
            .HasFilter("status IN ('Queued', 'Running')");

        builder.Ignore(j => j.IsFinished);
    }
}
