using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeachingAssistant.Domain.Usage;

namespace TeachingAssistant.Infrastructure.Persistence.Configurations;

public class ModelCallConfiguration : IEntityTypeConfiguration<ModelCall>
{
    public void Configure(EntityTypeBuilder<ModelCall> builder)
    {
        builder.ToTable("model_calls");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Purpose).HasConversion<string>();
        builder.Property(c => c.Error).HasMaxLength(1000);

        builder.HasIndex(c => c.CreatedAt);
        builder.HasIndex(c => new { c.Purpose, c.CreatedAt });
        builder.HasIndex(c => new { c.CourseId, c.AssignmentId });
    }
}
