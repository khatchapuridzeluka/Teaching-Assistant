using System.ComponentModel;
using System.Globalization;
using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Exceptions;
using TeachingAssistant.Application.Interfaces.Services;

namespace TeachingAssistant.Application.Tools;

public class ClassroomTools
{
    private readonly ICourseService _courseService;
    private readonly IAssignmentService _assignmentService;
    private readonly ISubmissionFetchService _fetchService;
    private readonly IStoredSubmissionService _storedService;
    private readonly ICourseSyncService _syncService;

    public ClassroomTools(
        ICourseService courseService, IAssignmentService assignmentService, ISubmissionFetchService fetchService,
        IStoredSubmissionService storedService, ICourseSyncService syncService)
    {
        _courseService = courseService;
        _assignmentService = assignmentService;
        _fetchService = fetchService;
        _storedService = storedService;
        _syncService = syncService;
    }

    [AgentTool("list_courses")]
    [Description("Lists the teacher's Google Classroom courses with their ids.")]
    public Task<string> ListCoursesAsync(CancellationToken cancellationToken)
    {
        return RunAsync(async () =>
        {
            var courses = await _courseService.GetCoursesAsync(cancellationToken);
            return courses.Count == 0
                ? "The teacher has no courses."
                : string.Join("\n", courses.Select(c => $"- {c.Name} (course id {c.Id}, {c.State})"));
        });
    }

    [AgentTool("list_assignments")]
    [Description("Lists the assignments of one course with their ids, titles and due dates.")]
    public Task<string> ListAssignmentsAsync(
        [Description("The id of the course")] string courseId, CancellationToken cancellationToken)
    {
        return RunAsync(async () =>
        {
            var assignments = await _assignmentService.GetAssignmentsAsync(courseId, cancellationToken);
            return assignments.Count == 0
                ? "This course has no assignments."
                : string.Join("\n", assignments.Select(a => $"- {a.Title} (assignment id {a.Id}, {Due(a.DueAt)})"));
        });
    }

    [AgentTool("sync_course")]
    [Description(
        "Syncs one course from Google Classroom, read-only and free (no AI call): its roster of students, its assignments and "
        + "the list of submissions with their Classroom grades, like the Sync button on the course page. It does not download "
        + "submission files; fetch_submissions does that. Use it when a course has no students synced yet, or when a student, "
        + "an assignment or a turn-in the teacher mentions is missing, then repeat the step that needed it.")]
    public Task<string> SyncCourseAsync(
        [Description("The id of the course")] string courseId, CancellationToken cancellationToken)
    {
        return RunAsync(async () =>
        {
            var summary = await _syncService.SyncAsync(courseId, cancellationToken);
            return $"Sync finished. Students: {summary.Students} ({summary.NewStudents} new, {summary.LeftStudents} left the course). "
                + $"Assignments: {summary.Assignments}. Submissions checked: {summary.Submissions}, of which {summary.NewSubmissions} "
                + $"were new; {summary.Graded} have a Classroom grade. Student ids can now be looked up with find_students.";
        });
    }

    [AgentTool("fetch_submissions")]
    [Description(
        "Downloads, read-only from Google Classroom, every turned-in submission of one assignment that is new or changed "
        + "since the last fetch, and stores it in the tool. It takes a while. Returns how many were stored, skipped or had a problem.")]
    public Task<string> FetchSubmissionsAsync(
        [Description("The id of the course")] string courseId,
        [Description("The id of the assignment")] string assignmentId,
        CancellationToken cancellationToken)
    {
        return RunAsync(async () =>
        {
            var summary = await _fetchService.FetchAsync(courseId, assignmentId, cancellationToken);
            var turnedIn = summary.Submissions - summary.NotSubmitted;
            return $"Fetch finished. Students on the assignment: {summary.Submissions}, of whom {turnedIn} turned in and "
                + $"{summary.NotSubmitted} did not. Of those turned in: {summary.NewSubmissions} stored for the first time, "
                + $"{summary.NewVersions} stored as a new version, {summary.Unchanged} unchanged and skipped. "
                + $"Problems noted during the fetch (a file that could not be downloaded or read, missing instructions): "
                + $"{summary.Problems.Count}; the teacher sees them with the Fetch button on the assignment page. "
                + $"New version of the task instructions: {(summary.NewInstructions ? "yes" : "no")}.";
        });
    }

    [AgentTool("list_submissions")]
    [Description(
        "Lists the submissions of one assignment that are stored in the tool (already fetched): the student's name, the "
        + "submission id, and for the latest version its number, status, whether code was extracted, when it was turned in and "
        + "how many files it has. It does not contact Google Classroom; if it returns nothing, fetch the submissions first.")]
    public Task<string> ListSubmissionsAsync(
        [Description("The id of the assignment")] string assignmentId, CancellationToken cancellationToken)
    {
        return RunAsync(async () =>
        {
            var submissions = await _storedService.GetByAssignmentAsync(assignmentId, cancellationToken);
            return submissions.Count == 0
                ? "No submissions of this assignment are stored yet. Fetch them first."
                : string.Join("\n", submissions.Select(Describe));
        });
    }

    private static string Describe(StoredSubmissionResponse submission)
    {
        var latest = submission.Versions.MaxBy(v => v.Number);
        if (latest is null)
        {
            return $"- {submission.StudentName} (submission id {submission.Id}): no stored version";
        }

        var turnedIn = latest.TurnedInAt is { } at
            ? $"turned in {at.ToUniversalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)} UTC"
            : "turn-in time unknown";
        return $"- {submission.StudentName} (submission id {submission.Id}): version {latest.Number} of {submission.Versions.Count}, "
            + $"status {latest.Status}, {(latest.HasCode ? "code extracted" : "no code extracted")}, {turnedIn}, "
            + $"{latest.Files.Count} {(latest.Files.Count == 1 ? "file" : "files")}";
    }

    private static async Task<string> RunAsync(Func<Task<string>> tool)
    {
        try
        {
            return await tool();
        }
        catch (Exception ex) when (ex is SubmissionSourceNotConnectedException or SubmissionSourceNotFoundException
            or SubmissionSourceUnavailableException)
        {
            return $"Failed: {ex.Message}";
        }
    }

    private static string Due(DateTimeOffset? dueAt)
    {
        return dueAt is { } due ? $"due {due.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}" : "no due date";
    }
}
