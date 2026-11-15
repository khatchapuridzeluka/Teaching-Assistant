using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Infrastructure.Persistence.Configurations;

public class AssignmentNoteConfiguration : IEntityTypeConfiguration<AssignmentNote>
{
    public void Configure(EntityTypeBuilder<AssignmentNote> builder)
    {
        builder.ToTable("assignment_notes");
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).ValueGeneratedNever();
        builder.Property(n => n.Title).HasMaxLength(AssignmentNote.MaxTitleLength);
        builder.Property(n => n.Detail).HasMaxLength(AssignmentNote.MaxDetailLength);
        builder.HasIndex(n => new { n.AssignmentId, n.IsDeleted, n.Position });
        builder.HasIndex(n => new { n.CourseId, n.IsDeleted });
    }
}
