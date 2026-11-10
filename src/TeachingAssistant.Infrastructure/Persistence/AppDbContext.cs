using Microsoft.EntityFrameworkCore;
using TeachingAssistant.Domain.Grading;
using TeachingAssistant.Domain.Ingestion;
using TeachingAssistant.Domain.Jobs;
using TeachingAssistant.Domain.Profiling;
using TeachingAssistant.Domain.Usage;

namespace TeachingAssistant.Infrastructure.Persistence;

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
    public DbSet<GradingReport> GradingReports => Set<GradingReport>();
    public DbSet<ReportMistake> ReportMistakes => Set<ReportMistake>();
    public DbSet<StudentMistake> StudentMistakes => Set<StudentMistake>();
    public DbSet<ModelCall> ModelCalls => Set<ModelCall>();
    public DbSet<MistakeAnalysis> MistakeAnalyses => Set<MistakeAnalysis>();
    public DbSet<Job> Jobs => Set<Job>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
}
