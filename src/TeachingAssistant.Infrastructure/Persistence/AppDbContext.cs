using Microsoft.EntityFrameworkCore;
using TeachingAssistant.Domain.Grading;
using TeachingAssistant.Domain.Ingestion;
using TeachingAssistant.Domain.Profiling;

namespace TeachingAssistant.Infrastructure.Persistence;

/// <summary>
/// EF Core context for the app's own data in Postgres. Entities are the Domain classes;
/// how they map to tables lives in Configurations/, so Domain stays free of EF.
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Submission> Submissions => Set<Submission>();
    public DbSet<SubmissionVersion> SubmissionVersions => Set<SubmissionVersion>();
    public DbSet<VersionContent> VersionContents => Set<VersionContent>();
    public DbSet<AssignmentInstructions> AssignmentInstructions => Set<AssignmentInstructions>();
    public DbSet<InstructionsVersion> InstructionsVersions => Set<InstructionsVersion>();
    public DbSet<InstructionsContent> InstructionsContents => Set<InstructionsContent>();
    public DbSet<GradeEvent> GradeEvents => Set<GradeEvent>();
    public DbSet<Student> Students => Set<Student>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();
    public DbSet<CourseAssignment> CourseAssignments => Set<CourseAssignment>();
    public DbSet<AiGrading> Gradings => Set<AiGrading>();
    public DbSet<GradingMistake> GradingMistakes => Set<GradingMistake>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
}
