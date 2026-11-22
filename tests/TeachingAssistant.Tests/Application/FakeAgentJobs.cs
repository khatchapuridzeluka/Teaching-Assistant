using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Domain.Profiling;
using TeachingAssistant.Application.Interfaces.Services;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Domain.Grading;
using TeachingAssistant.Domain.Ingestion;
using TeachingAssistant.Domain.Jobs;

namespace TeachingAssistant.Tests.Application;

internal sealed class FakeAgentJobs : IJobStore, IJobSignal, IGradingStore, IMistakeAnalysisStore, IStudentAnalysisService
{
    public Task<IReadOnlyList<TeachingAssistant.Application.Models.DiscardedDraft>> GetDiscardedAsync(string? courseId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public List<StudentAnalysisResponse> StudentAnalyses { get; } = [];

    public Task<StudentAnalysisResponse?> GetLatestAsync(
        string courseId, string studentId, StudentAnalysisKind kind, CancellationToken cancellationToken = default) =>
        Task.FromResult(StudentAnalyses.LastOrDefault(a => a.Kind == kind));

    Task<Guid> IStudentAnalysisService.AnalyzeAsync(
        string courseId, string studentId, StudentAnalysisKind kind, CancellationToken cancellationToken) =>
        throw new NotSupportedException();


    public List<MistakeAnalysis> Analyses { get; } = [];

    public Task<MistakeAnalysis?> GetLatestAsync(string courseId, string assignmentId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Analyses.LastOrDefault(a => a.CourseId == courseId && a.AssignmentId == assignmentId));

    public Task<MistakeAnalysis?> FindByClusterAsync(Guid clusterId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public void Add(MistakeAnalysis analysis) => Analyses.Add(analysis);

    public List<Job> Jobs { get; } = [];

    public List<JobSubject> Subjects { get; } = [];

    public List<AiGrading> Gradings { get; } = [];

    public int Notified { get; private set; }

    public void Notify() => Notified++;

    public Task<Job> AddOrGetActiveAsync(Job job, CancellationToken cancellationToken = default)
    {
        Jobs.Add(job);
        return Task.FromResult(job);
    }

    public Task<Job?> FindAsync(Guid jobId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Jobs.FirstOrDefault(j => j.Id == jobId));

    public Task<IReadOnlyList<JobSubject>> GetSubjectsAsync(IReadOnlyCollection<Guid> versionIds, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<JobSubject>>(Subjects.Where(s => versionIds.Contains(s.VersionId)).ToList());

    Task<AiGrading?> IGradingStore.FindAsync(Guid gradingId, CancellationToken cancellationToken) =>
        Task.FromResult(Gradings.FirstOrDefault(g => g.Id == gradingId));

    public Task<IReadOnlyList<AiGrading>> GetByVersionsAsync(IReadOnlyCollection<Guid> versionIds, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<AiGrading>>(Gradings.Where(g => versionIds.Contains(g.VersionId)).ToList());

    public Task<IReadOnlyList<GradingSummary>> GetSummariesAsync(
        SubmissionSource source, string courseId, string? assignmentId, string? studentId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public void Add(AiGrading grading) => Gradings.Add(grading);

    public Task<Job?> ClaimNextAsync(string workerId, DateTimeOffset now, TimeSpan lease, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<Job>> GetByAssignmentAsync(string assignmentId, int limit, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<Job>> GetExpiredLeasesAsync(DateTimeOffset now, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<Job>> GetRecentAsync(
        JobStatus? status, JobType? type, string? courseId, string? assignmentId, int limit, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyDictionary<JobStatus, int>> CountByStatusAsync(
        DateTimeOffset finishedSince, JobType? type, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<Job>> GetChildrenAsync(Guid parentId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<JobChildCount>> CountChildrenAsync(
        IReadOnlyCollection<Guid> parentIds, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<Job>> GetOpenGroupsAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
