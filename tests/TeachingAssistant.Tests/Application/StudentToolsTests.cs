using System.Reflection;
using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Exceptions;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Interfaces.Services;
using TeachingAssistant.Application.Jobs;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Application.Tools;
using TeachingAssistant.Domain.Grading;
using TeachingAssistant.Domain.Ingestion;
using TeachingAssistant.Domain.Jobs;
using TeachingAssistant.Domain.Profiling;

namespace TeachingAssistant.Tests.Application;

public class StudentToolsTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 11, 10, 0, 0, TimeSpan.Zero);

    private readonly Fakes _fakes = new();

    private StudentTools Tools()
    {
        return new StudentTools(_fakes, _fakes, _fakes, _fakes, _fakes, new AgentRunContext(), new FixedTime(Now.AddMinutes(1)));
    }

    [Fact]
    public void EveryToolIsMarkedWithItsName()
    {
        var names = typeof(StudentTools).GetMethods()
            .Select(m => m.GetCustomAttribute<AgentToolAttribute>()?.Name)
            .OfType<string>()
            .Order();

        Assert.Equal(["find_students", "grade_all_assignments_of_student", "grade_student_assignment", "list_student_assignments"], names);
    }

    [Fact]
    public async Task FindStudents_ReturnsEveryMatch_IgnoringCase()
    {
        var result = await Tools().FindStudentsAsync("course-1", "nino", TestContext.Current.CancellationToken);

        Assert.Equal(
            "2 students match; ask the teacher which one is meant:\n"
            + "- Nino Beridze (student id s-1)\n- Nino Kapanadze (student id s-2, no longer enrolled)",
            result);
    }

    [Fact]
    public async Task FindStudents_MatchesEveryWordOfTheName()
    {
        var result = await Tools().FindStudentsAsync("course-1", "Nino Kap", TestContext.Current.CancellationToken);

        Assert.Equal("One student matches:\n- Nino Kapanadze (student id s-2, no longer enrolled)", result);
    }

    [Fact]
    public async Task FindStudents_ListsEveryone_WhenNobodyMatches()
    {
        var result = await Tools().FindStudentsAsync("course-1", "Zurab", TestContext.Current.CancellationToken);

        Assert.StartsWith("No student's name contains 'Zurab'. The students of this course are:", result);
        Assert.Contains("- Giorgi Lomidze (student id s-3)", result);
    }

    [Fact]
    public async Task FindStudents_PointsToSyncCourse_WhenTheRosterIsEmpty()
    {
        _fakes.RosterSynced = false;

        var result = await Tools().FindStudentsAsync("course-1", "Nino", TestContext.Current.CancellationToken);

        Assert.Equal(
            "This course has no students synced yet. Call sync_course for this course (free, read-only), then call find_students again.",
            result);
    }

    [Fact]
    public async Task ListStudentAssignments_DescribesEachAssignmentForThatStudent()
    {
        var result = await Tools().ListStudentAssignmentsAsync("course-1", "s-1", TestContext.Current.CancellationToken);

        Assert.Equal(
            "Nino Beridze: 1 turned in, 1 missing, 1 late.\n"
            + "- Assignment 4 (assignment id a-4, due 2026-11-20): turned in late, graded in Classroom: 80 of 100\n"
            + "- Assignment 5 (assignment id a-5, no due date): missing, no grade",
            result);
    }

    [Fact]
    public async Task ListStudentAssignments_SaysWhenTheIdIsUnknown()
    {
        var result = await Tools().ListStudentAssignmentsAsync("course-1", "nobody", TestContext.Current.CancellationToken);

        Assert.Equal("No student with id nobody is in this course. Use find_students to look up the id by name.", result);
    }

    [Fact]
    public async Task Grade_FetchesFirst_ThenQueuesTheLatestVersion()
    {
        var submission = _fakes.AddSubmission("s-1", "Nino Beridze", versions: 2);

        var result = await Tools().GradeStudentAssignmentAsync("course-1", "a-4", "s-1", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal([("course-1", "a-4")], _fakes.Fetches);
        Assert.Equal([submission.LatestVersion!.Id], _fakes.Queued);
        Assert.StartsWith("Grading queued for Nino Beridze, version 2 (job id ", result);
    }

    [Fact]
    public async Task Grade_DoesNotStartAgain_WhenTheLatestVersionHasADraft()
    {
        var submission = _fakes.AddSubmission("s-1", "Nino Beridze", versions: 1);
        _fakes.Gradings.Add(new AiGrading(submission.LatestVersion!.Id, "model", "review", 72, 100, null, 1, [], [], "{}", [], Now));

        var result = await Tools().GradeStudentAssignmentAsync("course-1", "a-4", "s-1", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Empty(_fakes.Queued);
        Assert.Equal(
            "Not started: version 1 of Nino Beridze's submission already has an AI draft (score 72 of 100). "
            + "Call this again with regrade set to true only if the teacher asks to grade it again.",
            result);
    }

    [Fact]
    public async Task Regrade_QueuesAgain_EvenWhenTheLatestVersionHasADraft()
    {
        var submission = _fakes.AddSubmission("s-1", "Nino Beridze", versions: 1);
        _fakes.Gradings.Add(new AiGrading(submission.LatestVersion!.Id, "model", "review", 72, 100, null, 1, [], [], "{}", [], Now));

        var result = await Tools().GradeStudentAssignmentAsync("course-1", "a-4", "s-1", regrade: true, TestContext.Current.CancellationToken);

        Assert.Equal([("course-1", "a-4")], _fakes.Fetches);
        Assert.Equal([submission.LatestVersion.Id], _fakes.Queued);
        Assert.StartsWith("Grading again queued for Nino Beridze, version 1 (job id ", result);
        Assert.EndsWith("The current draft (score 72 of 100) stays in the history; the new one becomes current when it is done.", result);
    }

    [Fact]
    public async Task Regrade_WithoutADraft_IsAnOrdinaryGrading()
    {
        _fakes.AddSubmission("s-1", "Nino Beridze", versions: 1);

        var result = await Tools().GradeStudentAssignmentAsync("course-1", "a-4", "s-1", regrade: true, TestContext.Current.CancellationToken);

        Assert.StartsWith("Grading queued for Nino Beridze, version 1", result);
        Assert.DoesNotContain("history", result);
    }

    [Fact]
    public async Task Grade_SaysThereIsNothingToGrade_WhenTheStudentTurnedNothingIn()
    {
        _fakes.AddSubmission("s-2", "Nino Kapanadze", versions: 1);

        var result = await Tools().GradeStudentAssignmentAsync("course-1", "a-4", "s-1", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Empty(_fakes.Queued);
        Assert.Equal("Nothing to grade: after fetching, this student has no turned-in work stored for this assignment.", result);
    }

    [Fact]
    public async Task Grade_SaysAGradingIsAlreadyInProgress_InsteadOfClaimingANewOne()
    {
        _fakes.AddSubmission("s-1", "Nino Beridze", versions: 1);
        _fakes.ExistingJob = true;

        var result = await Tools().GradeStudentAssignmentAsync("course-1", "a-4", "s-1", cancellationToken: TestContext.Current.CancellationToken);

        Assert.StartsWith("A grading of Nino Beridze's version 1 is already in progress (job id ", result);
    }

    [Fact]
    public async Task GradeAllAssignments_QueuesTheStudentJob_WithTheRegradeChoice()
    {
        var result = await Tools().GradeAllAssignmentsOfStudentAsync("course-1", "s-1", regrade: true, TestContext.Current.CancellationToken);

        Assert.Equal([("course-1", "s-1", true)], _fakes.StudentGradings);
        Assert.StartsWith("Grading of all this student's assignments queued (job id ", result);
        Assert.Contains("including those that already have a draft", result);
    }

    [Fact]
    public async Task Grade_ReturnsTheReason_WhenTheGradingCannotStart()
    {
        _fakes.AddSubmission("s-1", "Nino Beridze", versions: 1);
        _fakes.QueueFailure = new InvalidRequestException("The assignment instructions are not stored yet.");

        var result = await Tools().GradeStudentAssignmentAsync("course-1", "a-4", "s-1", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("Failed: The assignment instructions are not stored yet.", result);
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class Fakes : IStudentService, ISubmissionFetchService, ISubmissionStore, IGradingStore, IJobService
    {
        public Task<TeachingAssistant.Application.Contracts.JobResponse> QueueEvaluationRunAsync(Guid runId, IReadOnlyList<Guid> resultIds, string? courseId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<TeachingAssistant.Application.Models.DiscardedDraft>> GetDiscardedAsync(string? courseId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public List<(string CourseId, string AssignmentId)> Fetches { get; } = [];

        public List<Submission> Submissions { get; } = [];

        public List<AiGrading> Gradings { get; } = [];

        public List<Guid> Queued { get; } = [];

        public Exception? QueueFailure { get; set; }

        public bool ExistingJob { get; set; }

        public bool RosterSynced { get; set; } = true;

        public List<(string CourseId, string StudentId, bool Regrade)> StudentGradings { get; } = [];

        public Submission AddSubmission(string studentId, string name, int versions)
        {
            var submission = new Submission(SubmissionSource.GoogleClassroom, "course-1", "a-4", studentId, name, $"ext-{studentId}", "folder", Now);
            for (var i = 0; i < versions; i++)
            {
                submission.AddVersion(VersionStatus.Ready, null, null, $"v{i + 1}", Now, Now, []);
            }

            Submissions.Add(submission);
            return submission;
        }

        public Task<CourseStudentsResponse> GetCourseStudentsAsync(string courseId, CancellationToken cancellationToken = default)
        {
            StudentAssignmentColumn[] assignments =
            [
                new("a-4", "Assignment 4", 100, new DateTimeOffset(2026, 11, 20, 19, 0, 0, TimeSpan.Zero)),
                new("a-5", "Assignment 5", 100, null),
            ];
            StudentAssignmentResult[] results =
            [
                new("a-4", "sub-4", "TURNED_IN", true, true, false, GradeStatus.Graded, 80),
                new("a-5", null, null, false, false, true, GradeStatus.Missing, null),
            ];
            CourseStudentResponse[] students = RosterSynced
                ?
                [
                    Student("s-1", "Nino Beridze", true, results),
                    Student("s-2", "Nino Kapanadze", false, []),
                    Student("s-3", "Giorgi Lomidze", true, []),
                ]
                : [];
            return Task.FromResult(new CourseStudentsResponse(Now, assignments, students));
        }

        private static CourseStudentResponse Student(string id, string name, bool active, IReadOnlyList<StudentAssignmentResult> results)
        {
            return new CourseStudentResponse(Guid.NewGuid(), id, name, active, 1, 1, 1, null, 0, 0, results);
        }

        public Task<StudentPhoto?> OpenPhotoAsync(string externalUserId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<FetchSummary> FetchAsync(string courseId, string assignmentId, CancellationToken cancellationToken = default)
        {
            Fetches.Add((courseId, assignmentId));
            return Task.FromResult(new FetchSummary(1, 0, 0, 1, 0, [], 1, false));
        }

        public Task<IReadOnlyList<Submission>> GetByAssignmentAsync(
            SubmissionSource source, string assignmentId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Submission>>(Submissions.Where(s => s.AssignmentId == assignmentId).ToList());

        public Task<IReadOnlyList<Submission>> GetByCourseAsync(
            SubmissionSource source, string courseId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<StoredFileLocation?> FindStoredFileAsync(Guid contentId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Submission?> FindByVersionAsync(Guid versionId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<string?> FindVersionPathAsync(Guid versionId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public void Add(Submission submission) => throw new NotSupportedException();

        public Task<IReadOnlyList<AiGrading>> GetByVersionsAsync(IReadOnlyCollection<Guid> versionIds, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AiGrading>>(Gradings.Where(g => versionIds.Contains(g.VersionId)).ToList());

        public Task<AiGrading?> FindAsync(Guid gradingId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<GradingSummary>> GetSummariesAsync(
            SubmissionSource source, string courseId, string? assignmentId, string? studentId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public void Add(AiGrading grading) => throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<JobResponse?> QueueGradeVersionAsync(Guid versionId, CancellationToken cancellationToken = default)
        {
            if (QueueFailure is not null)
            {
                throw QueueFailure;
            }

            Queued.Add(versionId);
            var job = new Job(JobType.GradeVersion, JobPayloads.Write(new GradeVersionPayload(versionId)), ExistingJob ? Now : Now.AddMinutes(1));
            return Task.FromResult<JobResponse?>(JobPayloads.ToResponse(job));
        }

        public Task<JobResponse?> GetAsync(Guid jobId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<JobResponse>> GetByAssignmentAsync(string assignmentId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<JobResponse?> CancelAsync(Guid jobId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<JobOverviewResponse> GetOverviewAsync(
            JobStatus? status, JobType? type, string? courseId, string? assignmentId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<JobResponse?> RetryAsync(Guid jobId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<GradeAllResponse> QueueGradeAssignmentAsync(string courseId, string assignmentId, bool regrade = false, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<JobOverviewItem>?> GetChildrenAsync(Guid jobId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<JobResponse> QueueGradeStudentAsync(string courseId, string studentId, bool regrade = false, CancellationToken cancellationToken = default)
        {
            StudentGradings.Add((courseId, studentId, regrade));
            var job = Job.Group(JobType.GradeStudent, JobPayloads.Write(new GradeStudentPayload(courseId, studentId)), Now.AddMinutes(1));
            return Task.FromResult(JobPayloads.ToResponse(job));
        }

        public Task<FetchStudentWorkResult> QueueStudentGradingsAsync(
            Guid parentId, string courseId, string studentId, int fetched, IReadOnlyList<string> fetchProblems,
            bool regrade = false, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<JobResponse> QueueAnalyzeMistakesAsync(string courseId, string assignmentId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<JobResponse> QueueAnalyzeStudentAsync(
            string courseId, string studentId, StudentAnalysisKind kind, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
