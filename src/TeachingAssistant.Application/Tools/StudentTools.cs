using System.ComponentModel;
using System.Globalization;
using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Exceptions;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Interfaces.Services;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Domain.Grading;
using TeachingAssistant.Domain.Ingestion;
using TeachingAssistant.Domain.Profiling;

namespace TeachingAssistant.Application.Tools;

public class StudentTools
{
    private readonly IStudentService _studentService;
    private readonly ISubmissionFetchService _fetchService;
    private readonly ISubmissionStore _submissions;
    private readonly IGradingStore _gradings;
    private readonly IJobService _jobService;
    private readonly AgentRunContext _context;
    private readonly TimeProvider _timeProvider;

    public StudentTools(
        IStudentService studentService, ISubmissionFetchService fetchService, ISubmissionStore submissions, IGradingStore gradings,
        IJobService jobService, AgentRunContext context, TimeProvider timeProvider)
    {
        _context = context;
        _timeProvider = timeProvider;
        _studentService = studentService;
        _fetchService = fetchService;
        _submissions = submissions;
        _gradings = gradings;
        _jobService = jobService;
    }

    [AgentTool("find_students")]
    [Description(
        "Finds the students of one course whose name contains the given name or part of it, ignoring case, and returns each "
        + "match with their student id. Several students can match; then all of them are returned and you should ask the teacher "
        + "which one is meant. If none match, it returns every student of the course instead.")]
    public Task<string> FindStudentsAsync(
        [Description("The id of the course")] string courseId,
        [Description("The student's name or part of it, for example a first name")] string name,
        CancellationToken cancellationToken)
    {
        return RunAsync(async () =>
        {
            var students = (await _studentService.GetCourseStudentsAsync(courseId, cancellationToken)).Students;
            if (students.Count == 0)
            {
                return "This course has no students synced yet.";
            }

            var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var matches = students
                .Where(s => words.Length > 0 && words.All(w => s.Name.Contains(w, StringComparison.CurrentCultureIgnoreCase)))
                .ToList();

            if (matches.Count == 0)
            {
                return $"No student's name contains '{name}'. The students of this course are:\n{string.Join("\n", students.Select(Describe))}";
            }

            var header = matches.Count == 1 ? "One student matches:" : $"{matches.Count} students match; ask the teacher which one is meant:";
            return $"{header}\n{string.Join("\n", matches.Select(Describe))}";
        });
    }

    [AgentTool("list_student_assignments")]
    [Description(
        "Lists every assignment of one course for one student, with the assignment id and due date, whether the student turned it "
        + "in, missed it or turned it in late, and the grade status and grades from Google Classroom.")]
    public Task<string> ListStudentAssignmentsAsync(
        [Description("The id of the course")] string courseId,
        [Description("The student id, as returned by find_students")] string studentId,
        CancellationToken cancellationToken)
    {
        return RunAsync(async () =>
        {
            var course = await _studentService.GetCourseStudentsAsync(courseId, cancellationToken);
            var student = course.Students.FirstOrDefault(s => s.ExternalUserId == studentId);
            if (student is null)
            {
                return $"No student with id {studentId} is in this course. Use find_students to look up the id by name.";
            }

            var results = student.Results.ToDictionary(r => r.AssignmentId);
            var lines = course.Assignments.Select(a => Describe(a, results.GetValueOrDefault(a.Id)));
            return $"{student.Name}: {student.TurnedIn} turned in, {student.Missing} missing, {student.Late} late.\n{string.Join("\n", lines)}";
        });
    }

    [AgentTool("grade_student_assignment")]
    [Description(
        "Starts an AI grading of one student's submission of one assignment. It first fetches the assignment's submissions from "
        + "Google Classroom so the latest version is graded, then queues the grading in the background and returns at once; the "
        + "draft appears on the assignment page for the teacher to review after about a minute. Each grading is a paid AI call, so "
        + "use it only when the teacher asks for grading. If the latest version already has an AI draft, nothing new is started "
        + "unless regrade is true.")]
    public Task<string> GradeStudentAssignmentAsync(
        [Description("The id of the course")] string courseId,
        [Description("The id of the assignment")] string assignmentId,
        [Description("The student id, as returned by find_students")] string studentId,
        [Description("true grades again even when the latest version already has an AI draft (the new draft becomes current, the earlier one stays in the history); set it only when the teacher explicitly asks to grade again or regrade")] bool regrade = false,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(async () =>
        {
            await _fetchService.FetchAsync(courseId, assignmentId, cancellationToken);

            var submission = (await _submissions.GetByAssignmentAsync(SubmissionSource.GoogleClassroom, assignmentId, cancellationToken))
                .FirstOrDefault(s => s.CourseId == courseId && s.StudentId == studentId);
            var version = submission?.LatestVersion;
            if (submission is null || version is null)
            {
                return "Nothing to grade: after fetching, this student has no turned-in work stored for this assignment.";
            }

            if (version.Status == VersionStatus.Failed)
            {
                return $"Nothing to grade: version {version.Number} of {submission.StudentName}'s submission could not be stored "
                    + "(the teacher sees why on the assignment page).";
            }

            var draft = (await _gradings.GetByVersionsAsync([version.Id], cancellationToken))
                .FirstOrDefault(g => g.Status == GradingStatus.Draft);
            if (draft is not null && !regrade)
            {
                return $"Not started: version {version.Number} of {submission.StudentName}'s submission already has an AI draft "
                    + $"({Score(draft)}). Call this again with regrade set to true only if the teacher asks to grade it again.";
            }

            var started = _timeProvider.GetUtcNow();
            var job = await _jobService.QueueGradeVersionAsync(version.Id, cancellationToken);
            if (job is null)
            {
                return "Nothing to grade: the stored version disappeared while starting the grading.";
            }

            _context.RecordQueuedJob(job.Id);
            _context.RecordLinks([new AgentLink(AgentLinkKind.Submission, $"{submission.StudentName}'s submission", courseId, assignmentId,
                submission.ExternalSubmissionId)]);
            if (job.CreatedAt < started)
            {
                return $"A grading of {submission.StudentName}'s version {version.Number} is already in progress (job id {job.Id}); "
                    + "nothing new was started. Wait for it if you need its result.";
            }

            var replaced = draft is null
                ? ""
                : $" The current draft ({Score(draft)}) stays in the history; the new one becomes current when it is done.";
            return $"{(draft is null ? "Grading" : "Grading again")} queued for {submission.StudentName}, version {version.Number} "
                + $"(job id {job.Id}). It runs in the background and takes about a minute; the draft then appears on the assignment "
                + $"page for the teacher to review.{replaced}";
        });
    }

    [AgentTool("grade_all_assignments_of_student")]
    [Description(
        "Starts AI gradings of every assignment one student turned in (like Grade all assignments on the student's profile): it "
        + "first fetches everything the student turned in from Google Classroom, then grades each assignment whose latest "
        + "version has no AI draft yet. All of it runs in the background under one job; this returns at once with that job id. "
        + "Each grading is a paid AI call, so use it only when the teacher asks to grade all of a student's work.")]
    public async Task<string> GradeAllAssignmentsOfStudentAsync(
        [Description("The id of the course")] string courseId,
        [Description("The student id, as returned by find_students")] string studentId,
        [Description("true also grades again every assignment whose latest version already has an AI draft (the new drafts become "
            + "current, the earlier ones stay in the history); set it only when the teacher explicitly asks to grade again or regrade")]
        bool regrade = false,
        CancellationToken cancellationToken = default)
    {
        var started = _timeProvider.GetUtcNow();
        var job = await _jobService.QueueGradeStudentAsync(courseId, studentId, regrade, cancellationToken);
        _context.RecordQueuedJob(job.Id);
        _context.RecordLinks([new AgentLink(AgentLinkKind.Student, "The student's profile", courseId, StudentId: studentId)]);
        if (job.CreatedAt < started)
        {
            return $"Grading all of this student's assignments is already in progress (job id {job.Id}); nothing new was started. "
                + "Wait for it if you need the results.";
        }

        var which = regrade ? "every assignment with stored work, including those that already have a draft" : "every assignment without an AI draft";
        return $"Grading of all this student's assignments queued (job id {job.Id}): it fetches everything they turned in, then grades "
            + $"{which}. It runs in the background, about a minute per assignment; the drafts appear on each assignment's page.";
    }

    private static string Describe(CourseStudentResponse student)
    {
        return $"- {student.Name} (student id {student.ExternalUserId}{(student.Active ? "" : ", no longer enrolled")})";
    }

    private static string Describe(StudentAssignmentColumn assignment, StudentAssignmentResult? result)
    {
        var due = assignment.DueAt is { } at ? $"due {at.ToUniversalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}" : "no due date";
        var state = result switch
        {
            null => "not turned in",
            { TurnedIn: true, Late: true } => "turned in late",
            { TurnedIn: true } => "turned in",
            { Missing: true } => "missing",
            _ => "not turned in",
        };
        var grade = result?.GradeStatus switch
        {
            GradeStatus.Graded => $"graded in Classroom: {Points(result.FinalGrade ?? result.InitialGrade)} of {Points(assignment.MaxPoints)}",
            GradeStatus.AwaitingGrade => "waiting for a grade",
            GradeStatus.NotDue => "not due yet",
            _ => "no grade",
        };
        return $"- {assignment.Title} (assignment id {assignment.Id}, {due}): {state}, {grade}";
    }

    private static string Score(AiGrading grading)
    {
        return grading.Score is { } score ? $"score {Points(score)} of {Points(grading.MaxScore)}" : "no score";
    }

    private static string Points(double? points)
    {
        return points?.ToString("0.##", CultureInfo.InvariantCulture) ?? "?";
    }

    private static async Task<string> RunAsync(Func<Task<string>> tool)
    {
        try
        {
            return await tool();
        }
        catch (Exception ex) when (ex is SubmissionSourceNotConnectedException or SubmissionSourceNotFoundException
            or SubmissionSourceUnavailableException or InvalidRequestException)
        {
            return $"Failed: {ex.Message}";
        }
    }
}
