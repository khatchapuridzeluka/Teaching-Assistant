using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Exceptions;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Interfaces.Services;
using TeachingAssistant.Application.Jobs;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Domain.Ingestion;
using TeachingAssistant.Domain.Jobs;

namespace TeachingAssistant.Tests.Application;

public class FetchStudentWorkJobHandlerTests
{
    private readonly FakeSubmissions _submissions = new();
    private readonly FakeFetch _fetch = new();
    private readonly FakeJobs _jobs = new();

    [Fact]
    public async Task FetchesOnlyTheAssignmentsTheStudentTurnedIn_KeepsGoingAfterAFailure_ThenQueuesTheGradings()
    {
        _submissions.Add("assignment-1", "ria", "RETURNED");
        _submissions.Add("assignment-2", "ria", "TURNED_IN");
        _submissions.Add("assignment-3", "ria", "TURNED_IN");
        _submissions.Add("assignment-4", "ria", "CREATED");
        _submissions.Add("assignment-1", "someone-else", "TURNED_IN");
        _fetch.Failing.Add("assignment-2");
        _fetch.ProblemFor["assignment-3"] = new FetchProblem("Ria", "the archive could not be opened");
        var parentId = Guid.NewGuid();

        var result = await new FetchStudentWorkJobHandler(_submissions, _fetch, _jobs)
            .RunAsync(JobPayloads.Write(new FetchStudentWorkPayload("course-1", "ria", parentId)), TestContext.Current.CancellationToken);

        Assert.Equal(["assignment-1", "assignment-2", "assignment-3"], _fetch.Fetched);
        Assert.Equal((parentId, 2), (_jobs.ParentId, _jobs.Fetched));
        Assert.Equal(2, _jobs.Problems.Count);
        Assert.Contains(_jobs.Problems, p => p.Contains("Google said no"));
        Assert.Contains(_jobs.Problems, p => p.Contains("archive could not be opened"));
        Assert.Contains("\"queued\":0", result);
    }

    private sealed class FakeSubmissions : ISubmissionStore
    {
        private readonly List<Submission> _submissions = [];

        public void Add(string assignmentId, string studentId, string state)
        {
            var submission = new Submission(SubmissionSource.GoogleClassroom, "course-1", assignmentId, studentId,
                studentId == "ria" ? "Ria" : "Someone", $"sub-{assignmentId}-{studentId}", "folder", DateTimeOffset.UtcNow);
            submission.RecordStatus(state, false, null, [], DateTimeOffset.UtcNow);
            _submissions.Add(submission);
        }

        public Task<IReadOnlyList<Submission>> GetByCourseAsync(SubmissionSource source, string courseId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Submission>>(_submissions);

        public Task<IReadOnlyList<Submission>> GetByAssignmentAsync(SubmissionSource source, string assignmentId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<StoredFileLocation?> FindStoredFileAsync(Guid contentId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Submission?> FindByVersionAsync(Guid versionId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<string?> FindVersionPathAsync(Guid versionId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public void Add(Submission submission) => _submissions.Add(submission);

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeFetch : ISubmissionFetchService
    {
        public List<string> Fetched { get; } = [];
        public HashSet<string> Failing { get; } = [];
        public Dictionary<string, FetchProblem> ProblemFor { get; } = [];

        public Task<FetchSummary> FetchAsync(string courseId, string assignmentId, CancellationToken cancellationToken = default)
        {
            Fetched.Add(assignmentId);
            if (Failing.Contains(assignmentId))
            {
                throw new InvalidRequestException("Google said no.");
            }

            var problems = ProblemFor.TryGetValue(assignmentId, out var problem) ? new[] { problem } : [];
            return Task.FromResult(new FetchSummary(1, 0, 1, 0, 0, problems, 1, false));
        }
    }

    private sealed class FakeJobs : IJobService
    {
        public Guid ParentId { get; private set; }
        public int Fetched { get; private set; }
        public List<string> Problems { get; } = [];

        public Task<FetchStudentWorkResult> QueueStudentGradingsAsync(
            Guid parentId, string courseId, string studentId, int fetched, IReadOnlyList<string> fetchProblems,
            CancellationToken cancellationToken = default)
        {
            ParentId = parentId;
            Fetched = fetched;
            Problems.AddRange(fetchProblems);
            return Task.FromResult(new FetchStudentWorkResult(fetched, 0, 0, 0, 0, 0, fetchProblems));
        }

        public Task<JobResponse?> QueueGradeVersionAsync(Guid versionId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<JobResponse?> GetAsync(Guid jobId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<JobResponse>> GetByAssignmentAsync(string assignmentId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<JobResponse?> CancelAsync(Guid jobId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<JobOverviewResponse> GetOverviewAsync(
            JobStatus? status, JobType? type, string? courseId, string? assignmentId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<JobResponse?> RetryAsync(Guid jobId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<GradeAllResponse> QueueGradeAssignmentAsync(string courseId, string assignmentId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<JobOverviewItem>?> GetChildrenAsync(Guid jobId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<JobResponse> QueueGradeStudentAsync(string courseId, string studentId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<JobResponse> QueueAnalyzeMistakesAsync(string courseId, string assignmentId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<JobResponse> QueueAnalyzeStudentAsync(
            string courseId, string studentId, TeachingAssistant.Domain.Profiling.StudentAnalysisKind kind,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
