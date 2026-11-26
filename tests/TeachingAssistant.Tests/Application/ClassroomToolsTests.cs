using System.Reflection;
using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Exceptions;
using TeachingAssistant.Application.Interfaces.Services;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Application.Tools;
using TeachingAssistant.Domain.Grading;
using TeachingAssistant.Domain.Ingestion;

namespace TeachingAssistant.Tests.Application;

public class ClassroomToolsTests
{
    private readonly FakeServices _services = new();
    private readonly FakeGradingStore _gradings = new();

    private ClassroomTools Tools()
    {
        return new ClassroomTools(_services, _services, _services, _services, _services, _gradings);
    }

    [Fact]
    public void EveryToolIsMarkedWithAUniqueName()
    {
        var names = typeof(ClassroomTools).GetMethods()
            .Select(m => m.GetCustomAttribute<AgentToolAttribute>()?.Name)
            .OfType<string>()
            .Order();

        Assert.Equal(["fetch_submissions", "list_assignments", "list_courses", "list_submissions", "sync_course"], names);
    }

    [Fact]
    public async Task SyncCourse_SyncsThatCourse_AndReturnsCounts()
    {
        var result = await Tools().SyncCourseAsync("course-1", TestContext.Current.CancellationToken);

        Assert.Equal("course-1", Assert.Single(_services.Syncs));
        Assert.Equal(
            "Sync finished. Students: 9 (9 new, 0 left the course). Assignments: 3. Submissions checked: 20, of which 7 were new; "
            + "4 have a Classroom grade. Student ids can now be looked up with find_students.",
            result);
    }

    [Fact]
    public async Task SyncCourse_ReturnsAFailureToTheModel_InsteadOfThrowing()
    {
        _services.Failure = new SubmissionSourceNotConnectedException("Google account not connected.");

        var result = await Tools().SyncCourseAsync("course-1", TestContext.Current.CancellationToken);

        Assert.Equal("Failed: Google account not connected.", result);
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
        Assert.Contains("Students on the assignment: 5, of whom 2 turned in and 3 did not", result);
        Assert.Contains("2 stored for the first time, 1 stored as a new version, 2 unchanged", result);
        Assert.Contains("Problems noted during the fetch (a file that could not be downloaded or read, missing instructions): 1", result);
        Assert.DoesNotContain("Nino", result);
    }

    [Fact]
    public async Task ListSubmissions_DescribesTheLatestStoredVersionOfEachStudent()
    {
        var result = await Tools().ListSubmissionsAsync("course-1", "a-4", TestContext.Current.CancellationToken);

        Assert.Equal("a-4", _services.ListedAssignmentId);
        Assert.Equal(
            $"- Nino (student id student-1, submission id {FakeServices.SubmissionId}): version 2 of 2, status Ready, code extracted, "
            + "turned in 2026-11-19 15:30 UTC, 1 file; no AI draft yet",
            result);
    }

    [Fact]
    public async Task ListSubmissions_ShowsTheCurrentDraft_ItsScore_AndWhetherTheTeacherApprovedIt()
    {
        var older = new AiGrading(Guid.NewGuid(), "model", "review", 40, 100, null, 1, [], [], "{}", [], DateTimeOffset.UtcNow.AddMinutes(-5));
        var current = new AiGrading(Guid.NewGuid(), "model", "review", 82.5, 100, null, 1, [], [], "{}", [], DateTimeOffset.UtcNow);
        _gradings.AddDraft(older, "a-4", "external-1", "student-1", 1);
        _gradings.AddDraft(current, "a-4", "external-1", "student-1", 1, ReportStatus.Approved);

        var result = await Tools().ListSubmissionsAsync("course-1", "a-4", TestContext.Current.CancellationToken);

        Assert.EndsWith("; AI draft of version 1: score 82.5 / 100, approved by the teacher; version 2 is newer and has no draft yet", result);
    }

    [Fact]
    public async Task ListSubmissions_SaysToFetchFirst_WhenNothingIsStored()
    {
        _services.Stored = [];

        var result = await Tools().ListSubmissionsAsync("course-1", "a-4", TestContext.Current.CancellationToken);

        Assert.Equal("No submissions of this assignment are stored yet. Fetch them first.", result);
    }

    [Fact]
    public async Task AFailure_IsReturnedToTheModel_InsteadOfThrown()
    {
        _services.Failure = new SubmissionSourceNotConnectedException("Google account not connected.");

        var result = await Tools().FetchSubmissionsAsync("course-1", "a-4", TestContext.Current.CancellationToken);

        Assert.Equal("Failed: Google account not connected.", result);
    }

    private sealed class FakeServices : ICourseService, IAssignmentService, ISubmissionFetchService, IStoredSubmissionService, ICourseSyncService
    {
        public static readonly Guid SubmissionId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        public List<(string CourseId, string AssignmentId)> Fetches { get; } = [];

        public List<string> Syncs { get; } = [];

        public Task<CourseSyncSummary> SyncAsync(string courseId, CancellationToken cancellationToken = default)
        {
            Syncs.Add(courseId);
            if (Failure is not null)
            {
                throw Failure;
            }

            return Task.FromResult(new CourseSyncSummary
            {
                Students = 9,
                NewStudents = 9,
                LeftStudents = 0,
                Assignments = 3,
                Submissions = 20,
                NewSubmissions = 7,
                Graded = 4,
                PhotosUpdated = 9,
                PhotosFailed = 0,
            });
        }

        public string? ListedAssignmentId { get; private set; }

        public IReadOnlyList<StoredSubmissionResponse> Stored { get; set; } =
        [
            new StoredSubmissionResponse
            {
                Id = SubmissionId,
                ExternalSubmissionId = "external-1",
                StudentId = "student-1",
                StudentName = "Nino",
                Versions = [
                    Version(1, new DateTimeOffset(2026, 11, 18, 9, 0, 0, TimeSpan.Zero)),
                    Version(2, new DateTimeOffset(2026, 11, 19, 19, 30, 0, TimeSpan.FromHours(4))),
                ],
            },
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
            var file = new StoredFileResponse
            {
                Id = Guid.NewGuid(),
                Name = "work.zip",
                Kind = ContentKind.CodeArchive,
                MimeType = "application/zip",
                SizeBytes = 1024,
                Status = ContentStatus.Stored,
                Stored = true,
            };
            return new StoredVersionResponse
            {
                Id = Guid.NewGuid(),
                Number = number,
                Status = VersionStatus.Ready,
                HasCode = true,
                FetchedAt = turnedInAt,
                TurnedInAt = turnedInAt,
                Files = [file],
            };
        }

        public Exception? Failure { get; set; }

        public Task<IReadOnlyList<CourseResponse>> GetCoursesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<CourseResponse>>([new CourseResponse
            {
                Id = "course-1",
                Name = "Java II",
                State = "ACTIVE",
            }]);
        }

        public Task<IReadOnlyList<AssignmentResponse>> GetAssignmentsAsync(string courseId, CancellationToken cancellationToken = default)
        {
            var due = new DateTimeOffset(2026, 11, 20, 19, 0, 0, TimeSpan.Zero);
            return Task.FromResult<IReadOnlyList<AssignmentResponse>>(
            [
                new AssignmentResponse
                {
                    Id = "a-4",
                    CourseId = courseId,
                    Title = "Assignment 4",
                    WorkType = "ASSIGNMENT",
                    MaxPoints = 100,
                    DueAt = due,
                    Materials = [],
                },
                new AssignmentResponse
                {
                    Id = "a-5",
                    CourseId = courseId,
                    Title = "Assignment 5",
                    WorkType = "ASSIGNMENT",
                    MaxPoints = 100,
                    Materials = [],
                },
            ]);
        }

        public Task<FetchSummary> FetchAsync(string courseId, string assignmentId, CancellationToken cancellationToken = default)
        {
            Fetches.Add((courseId, assignmentId));
            if (Failure is not null)
            {
                throw Failure;
            }

            return Task.FromResult(new FetchSummary
            {
                Submissions = 5,
                NewSubmissions = 2,
                NewVersions = 1,
                Unchanged = 2,
                NotSubmitted = 3,
                Problems = [new FetchProblem
                {
                    StudentName = "Nino",
                    Detail = "The zip could not be opened.",
                }],
                InstructionsVersion = 1,
                NewInstructions = false,
            });
        }
    }
}
