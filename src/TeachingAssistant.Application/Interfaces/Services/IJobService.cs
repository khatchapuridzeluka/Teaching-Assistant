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

    Task<GradeAllResponse> QueueGradeAssignmentAsync(
        string courseId, string assignmentId, bool regrade = false, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<JobOverviewItem>?> GetChildrenAsync(Guid jobId, CancellationToken cancellationToken = default);

    Task<JobResponse> QueueGradeStudentAsync(string courseId, string studentId, bool regrade = false, CancellationToken cancellationToken = default);

    Task<FetchStudentWorkResult> QueueStudentGradingsAsync(
        Guid parentId, string courseId, string studentId, int fetched, IReadOnlyList<string> fetchProblems,
        bool regrade = false, CancellationToken cancellationToken = default);

    /// <summary>Queues an evaluation run: one group job with one side grading per result.</summary>
    Task<JobResponse> QueueEvaluationRunAsync(
        Guid runId, IReadOnlyList<Guid> resultIds, string? courseId, CancellationToken cancellationToken = default);

    Task<JobResponse> QueueAnalyzeMistakesAsync(string courseId, string assignmentId, CancellationToken cancellationToken = default);

    Task<JobResponse> QueueGenerateTestsAsync(
        string courseId, string assignmentId, GenerateTestsRequest request, CancellationToken cancellationToken = default);

    Task<JobResponse> QueueGenerateTestCodeAsync(
        string courseId, string assignmentId, Guid testSetId, CancellationToken cancellationToken = default);

    Task<JobResponse> QueueRunTestsAsync(
        string courseId, string assignmentId, Guid testSetId, Guid versionId, CancellationToken cancellationToken = default);

    Task<JobResponse> QueueAnalyzeStudentAsync(
        string courseId, string studentId, StudentAnalysisKind kind, CancellationToken cancellationToken = default);
}
