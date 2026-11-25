using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Interfaces.Services;
using TeachingAssistant.Application.Jobs;
using TeachingAssistant.Domain.Jobs;
using TeachingAssistant.Domain.Profiling;

namespace TeachingAssistant.Tests.Application;

internal sealed class FakeTestCodeJobs : IJobService
{
    public List<Guid> CodeJobs { get; } = [];

    public Task<JobResponse> QueueGenerateTestCodeAsync(
        string courseId, string assignmentId, Guid testSetId, CancellationToken cancellationToken = default)
    {
        CodeJobs.Add(testSetId);
        var job = new Job(JobType.GenerateTestCode, JobPayloads.Write(new GenerateTestCodePayload { TestSetId = testSetId }),
            DateTimeOffset.UtcNow, courseId: courseId, assignmentId: assignmentId);
        return Task.FromResult(JobPayloads.ToResponse(job));
    }

    public List<Guid> RunJobs { get; } = [];

    public List<bool> NewAdapterJobs { get; } = [];

    public Task<JobResponse> QueueRunTestsAsync(
        string courseId, string assignmentId, Guid testSetId, Guid versionId, bool newAdapter = false,
        CancellationToken cancellationToken = default)
    {
        RunJobs.Add(versionId);
        NewAdapterJobs.Add(newAdapter);
        var job = new Job(JobType.RunTests, JobPayloads.Write(new RunTestsPayload { TestSetId = testSetId, VersionId = versionId }),
            DateTimeOffset.UtcNow, courseId: courseId, assignmentId: assignmentId);
        return Task.FromResult(JobPayloads.ToResponse(job));
    }

    public Task<JobResponse?> QueueGradeVersionAsync(Guid versionId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<JobResponse?> GetAsync(Guid jobId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<IReadOnlyList<JobResponse>> GetByAssignmentAsync(string assignmentId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<JobResponse?> CancelAsync(Guid jobId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<JobOverviewResponse> GetOverviewAsync(
        JobStatus? status, JobType? type, string? courseId, string? assignmentId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<JobResponse?> RetryAsync(Guid jobId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<GradeAllResponse> QueueGradeAssignmentAsync(
        string courseId, string assignmentId, bool regrade = false, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<JobOverviewItem>?> GetChildrenAsync(Guid jobId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<JobResponse> QueueGradeStudentAsync(
        string courseId, string studentId, bool regrade = false, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<FetchStudentWorkResult> QueueStudentGradingsAsync(
        Guid parentId, string courseId, string studentId, int fetched, IReadOnlyList<string> fetchProblems, bool regrade = false,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<JobResponse> QueueEvaluationRunAsync(
        Guid runId, IReadOnlyList<Guid> resultIds, string? courseId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<JobResponse> QueueAnalyzeMistakesAsync(string courseId, string assignmentId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public List<GenerateTestsRequest> GenerateJobs { get; } = [];

    public Task<JobResponse> QueueGenerateTestsAsync(
        string courseId, string assignmentId, GenerateTestsRequest request, CancellationToken cancellationToken = default)
    {
        GenerateJobs.Add(request);
        var job = new Job(JobType.GenerateTests, JobPayloads.Write(new GenerateTestsPayload
        {
            CourseId = courseId,
            AssignmentId = assignmentId,
            Request = request,
        }), DateTimeOffset.UtcNow, courseId: courseId, assignmentId: assignmentId);
        return Task.FromResult(JobPayloads.ToResponse(job));
    }

    public Task<JobResponse> QueueAnalyzeStudentAsync(
        string courseId, string studentId, StudentAnalysisKind kind, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}
