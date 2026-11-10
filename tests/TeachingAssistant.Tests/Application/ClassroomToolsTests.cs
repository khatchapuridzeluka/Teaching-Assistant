using System.Reflection;
using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Exceptions;
using TeachingAssistant.Application.Interfaces.Services;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Application.Tools;
using TeachingAssistant.Domain.Ingestion;

namespace TeachingAssistant.Tests.Application;

public class ClassroomToolsTests
{
    private readonly FakeServices _services = new();

    private ClassroomTools Tools()
    {
        return new ClassroomTools(_services, _services, _services, _services);
    }

    [Fact]
    public void EveryToolIsMarkedWithAUniqueName()
    {
        var names = typeof(ClassroomTools).GetMethods()
            .Select(m => m.GetCustomAttribute<AgentToolAttribute>()?.Name)
            .OfType<string>()
            .Order();

        Assert.Equal(["fetch_submissions", "list_assignments", "list_courses", "list_submissions"], names);
    }

    [Fact]
    public async Task ListCourses_ReturnsNamesAndIds()
    {
        var result = await Tools().ListCoursesAsync(TestContext.Current.CancellationToken);

        Assert.Equal("- Java II (course id course-1, ACTIVE)", result);
    }

    [Fact]
    public async Task ListAssignments_ReturnsTitlesIdsAndDueDates()
    {
        var result = await Tools().ListAssignmentsAsync("course-1", TestContext.Current.CancellationToken);

        Assert.Equal("- Assignment 4 (assignment id a-4, due 2026-11-20)\n- Assignment 5 (assignment id a-5, no due date)", result);
    }

    [Fact]
    public async Task FetchSubmissions_FetchesThatAssignment_AndReturnsCountsWithoutNames()
    {
        var result = await Tools().FetchSubmissionsAsync("course-1", "a-4", TestContext.Current.CancellationToken);

        Assert.Equal(("course-1", "a-4"), Assert.Single(_services.Fetches));
        Assert.Contains("New submissions stored: 2", result);
        Assert.Contains("Submissions with a problem: 1", result);
        Assert.DoesNotContain("Nino", result);
    }

    [Fact]
    public async Task ListSubmissions_DescribesTheLatestStoredVersionOfEachStudent()
    {
        var result = await Tools().ListSubmissionsAsync("a-4", TestContext.Current.CancellationToken);

        Assert.Equal("a-4", _services.ListedAssignmentId);
        Assert.Equal(
            $"- Nino (submission id {FakeServices.SubmissionId}): version 2 of 2, status Ready, code extracted, "
            + "turned in 2026-11-19 15:30 UTC, 1 file",
            result);
    }

    [Fact]
    public async Task ListSubmissions_SaysToFetchFirst_WhenNothingIsStored()
    {
        _services.Stored = [];

        var result = await Tools().ListSubmissionsAsync("a-4", TestContext.Current.CancellationToken);

        Assert.Equal("No submissions of this assignment are stored yet. Fetch them first.", result);
    }

    [Fact]
    public async Task AFailure_IsReturnedToTheModel_InsteadOfThrown()
    {
        _services.Failure = new SubmissionSourceNotConnectedException("Google account not connected.");

        var result = await Tools().FetchSubmissionsAsync("course-1", "a-4", TestContext.Current.CancellationToken);

        Assert.Equal("Failed: Google account not connected.", result);
    }

    private sealed class FakeServices : ICourseService, IAssignmentService, ISubmissionFetchService, IStoredSubmissionService
    {
        public static readonly Guid SubmissionId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        public List<(string CourseId, string AssignmentId)> Fetches { get; } = [];

        public string? ListedAssignmentId { get; private set; }

        public IReadOnlyList<StoredSubmissionResponse> Stored { get; set; } =
        [
            new StoredSubmissionResponse(SubmissionId, "external-1", "Nino",
            [
                Version(1, new DateTimeOffset(2026, 11, 18, 9, 0, 0, TimeSpan.Zero)),
                Version(2, new DateTimeOffset(2026, 11, 19, 19, 30, 0, TimeSpan.FromHours(4))),
            ]),
        ];

        public Task<IReadOnlyList<StoredSubmissionResponse>> GetByAssignmentAsync(string assignmentId, CancellationToken cancellationToken = default)
        {
            ListedAssignmentId = assignmentId;
            return Task.FromResult(Stored);
        }

        public Task<StoredFileContent?> OpenFileAsync(Guid contentId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ExtractionSummary> ReExtractAsync(string assignmentId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<CodeFileResponse>?> ListCodeAsync(Guid versionId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Stream?> OpenCodeFileAsync(Guid versionId, string path, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        private static StoredVersionResponse Version(int number, DateTimeOffset turnedInAt)
        {
            var file = new StoredFileResponse(Guid.NewGuid(), "work.zip", ContentKind.CodeArchive, "application/zip", 1024, ContentStatus.Stored, true);
            return new StoredVersionResponse(Guid.NewGuid(), number, VersionStatus.Ready, null, true, null, turnedInAt, turnedInAt, [file]);
        }

        public Exception? Failure { get; set; }

        public Task<IReadOnlyList<CourseResponse>> GetCoursesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<CourseResponse>>([new CourseResponse("course-1", "Java II", null, "ACTIVE", null)]);
        }

        public Task<IReadOnlyList<AssignmentResponse>> GetAssignmentsAsync(string courseId, CancellationToken cancellationToken = default)
        {
            var due = new DateTimeOffset(2026, 11, 20, 19, 0, 0, TimeSpan.Zero);
            return Task.FromResult<IReadOnlyList<AssignmentResponse>>(
            [
                new AssignmentResponse("a-4", courseId, "Assignment 4", null, "ASSIGNMENT", 100, due, null, null, []),
                new AssignmentResponse("a-5", courseId, "Assignment 5", null, "ASSIGNMENT", 100, null, null, null, []),
            ]);
        }

        public Task<FetchSummary> FetchAsync(string courseId, string assignmentId, CancellationToken cancellationToken = default)
        {
            Fetches.Add((courseId, assignmentId));
            if (Failure is not null)
            {
                throw Failure;
            }

            return Task.FromResult(new FetchSummary(5, 2, 1, 2, 3, [new FetchProblem("Nino", "The zip could not be opened.")], 1, false));
        }
    }
}
