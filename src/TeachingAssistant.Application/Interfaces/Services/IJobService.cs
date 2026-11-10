using TeachingAssistant.Domain.Profiling;
using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Jobs;
using TeachingAssistant.Domain.Jobs;

namespace TeachingAssistant.Application.Interfaces.Services;

public interface IJobService
{
    Task<JobResponse?> QueueGradeVersionAsync(Guid versionId, CancellationToken cancellationToken = default);

    Task<JobResponse?> GetAsync(Guid jobId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<JobResponse>> GetByAssignmentAsync(string assignmentId, CancellationToken cancellationToken = default);

    Task<JobResponse?> CancelAsync(Guid jobId, CancellationToken cancellationToken = default);

    Task<JobOverviewResponse> GetOverviewAsync(
        JobStatus? status, JobType? type, string? courseId, string? assignmentId, CancellationToken cancellationToken = default);

    Task<JobResponse?> RetryAsync(Guid jobId, CancellationToken cancellationToken = default);

    Task<GradeAllResponse> QueueGradeAssignmentAsync(string courseId, string assignmentId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<JobOverviewItem>?> GetChildrenAsync(Guid jobId, CancellationToken cancellationToken = default);

    Task<JobResponse> QueueGradeStudentAsync(string courseId, string studentId, CancellationToken cancellationToken = default);

    Task<FetchStudentWorkResult> QueueStudentGradingsAsync(
        Guid parentId, string courseId, string studentId, int fetched, IReadOnlyList<string> fetchProblems,
        CancellationToken cancellationToken = default);

    Task<JobResponse> QueueAnalyzeMistakesAsync(string courseId, string assignmentId, CancellationToken cancellationToken = default);

    Task<JobResponse> QueueAnalyzeStudentAsync(
        string courseId, string studentId, StudentAnalysisKind kind, CancellationToken cancellationToken = default);
}

public interface IJobProcessor
{
    Task<ClaimedJob?> ClaimNextAsync(string workerId, TimeSpan lease, CancellationToken cancellationToken = default);

    Task<bool> HeartbeatAsync(Guid jobId, TimeSpan lease, CancellationToken cancellationToken = default);

    Task CompleteAsync(Guid jobId, string? result, CancellationToken cancellationToken = default);

    Task FailAsync(Guid jobId, Exception exception, CancellationToken cancellationToken = default);

    Task ConfirmCanceledAsync(Guid jobId, CancellationToken cancellationToken = default);

    Task<int> RecoverExpiredLeasesAsync(CancellationToken cancellationToken = default);

    Task<int> ReconcileGroupsAsync(CancellationToken cancellationToken = default);
}

public record ClaimedJob(Guid Id, JobType Type, string Payload);
