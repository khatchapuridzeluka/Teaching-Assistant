using Microsoft.EntityFrameworkCore;
using TeachingAssistant.Domain.Agents;
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
    public DbSet<GradingStrength> GradingStrengths => Set<GradingStrength>();
    public DbSet<ReportStrength> ReportStrengths => Set<ReportStrength>();
    public DbSet<StudentStrength> StudentStrengths => Set<StudentStrength>();
    public DbSet<StudentAnalysis> StudentAnalyses => Set<StudentAnalysis>();
    public DbSet<ModelCall> ModelCalls => Set<ModelCall>();
    public DbSet<MistakeAnalysis> MistakeAnalyses => Set<MistakeAnalysis>();
    public DbSet<GradingPrinciple> GradingPrinciples => Set<GradingPrinciple>();
    public DbSet<CourseGuidance> CourseGuidance => Set<CourseGuidance>();
    public DbSet<PolicyRule> PolicyRules => Set<PolicyRule>();
    public DbSet<AssignmentNote> AssignmentNotes => Set<AssignmentNote>();
    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<AgentConversation> AgentConversations => Set<AgentConversation>();
    public DbSet<AgentRun> AgentRuns => Set<AgentRun>();
    public DbSet<AgentMessage> AgentMessages => Set<AgentMessage>();
    public DbSet<EvaluationRun> EvaluationRuns => Set<EvaluationRun>();
    public DbSet<EvaluationRunResult> EvaluationRunResults => Set<EvaluationRunResult>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
}
