using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Interfaces.Services;
using TeachingAssistant.Domain.Grading;
using TeachingAssistant.Domain.Ingestion;
using TeachingAssistant.Domain.Profiling;

namespace TeachingAssistant.Application.Services;

/// <summary>
/// A course's students table: each student's state and grades per assignment and their overall score, computed from
/// what the last course sync (or assignment fetch) stored. Reads the database only, so it is instant and works offline.
/// </summary>
public class StudentService : IStudentService
{
    private readonly IStudentStore _students;
    private readonly ICourseAssignmentStore _assignments;
    private readonly ISubmissionStore _submissions;
    private readonly TimeProvider _timeProvider;

    public StudentService(IStudentStore students, ICourseAssignmentStore assignments, ISubmissionStore submissions, TimeProvider timeProvider)
    {
        _students = students;
        _assignments = assignments;
        _submissions = submissions;
        _timeProvider = timeProvider;
    }

    public async Task<CourseStudentsResponse> GetCourseStudentsAsync(string courseId, CancellationToken cancellationToken = default)
    {
        // One after another: the stores share one database context.
        var students = await _students.GetByCourseAsync(SubmissionSource.GoogleClassroom, courseId, cancellationToken);
        var assignments = (await _assignments.GetByCourseAsync(SubmissionSource.GoogleClassroom, courseId, cancellationToken))
            .OrderBy(a => a.DueAt ?? DateTimeOffset.MaxValue)
            .ThenBy(a => a.Title)
            .ToList();
        var submissions = (await _submissions.GetByCourseAsync(SubmissionSource.GoogleClassroom, courseId, cancellationToken))
            .ToLookup(s => s.StudentId);
        var now = _timeProvider.GetUtcNow();

        var rows = students
            .Select(student => Row(student, courseId, assignments, submissions[student.ExternalUserId].ToList(), now))
            .OrderByDescending(r => r.Active)
            .ThenBy(r => r.Name)
            .ToList();

        return new CourseStudentsResponse(
            assignments.Count == 0 ? null : assignments.Max(a => a.SyncedAt),
            assignments.Select(a => new StudentAssignmentColumn(a.ExternalAssignmentId, a.Title, a.MaxPoints, a.DueAt)).ToList(),
            rows);
    }

    private static CourseStudentResponse Row(
        Student student, string courseId, List<CourseAssignment> assignments, List<Submission> submissions, DateTimeOffset now)
    {
        var byAssignment = submissions.ToDictionary(s => s.AssignmentId);
        var results = new List<StudentAssignmentResult>();
        var scored = new List<AssignmentResult>();

        foreach (var assignment in assignments)
        {
            // No submission at all (e.g. the student joined after a sync) is treated like work never handed in.
            var submission = byAssignment.GetValueOrDefault(assignment.ExternalAssignmentId);
            var result = new AssignmentResult(assignment.MaxPoints, assignment.DueAt, submission?.FinalGrade, submission?.IsTurnedIn ?? false);
            scored.Add(result);
            var returns = (submission?.GradeEvents ?? [])
                .Where(e => e.Kind == GradeEventKind.AssignedGrade)
                .OrderBy(e => e.GradedAt)
                .Select(e => new GradeReturn(e.PointsEarned, e.GradedAt))
                .ToList();
            results.Add(new StudentAssignmentResult(assignment.ExternalAssignmentId, submission?.ExternalSubmissionId,
                submission?.State, result.TurnedIn, submission?.Late ?? false, result.IsMissing(now),
                submission?.InitialGrade, submission?.FinalGrade, returns));
        }

        var overall = OverallScore.Compute(scored, now);
        return new CourseStudentResponse(student.Id, student.ExternalUserId, student.Name, student.IsEnrolledIn(courseId),
            results.Count(r => r.TurnedIn), results.Count(r => r.Missing), results.Count(r => r.TurnedIn && r.Late),
            results.Count(r => r.InitialGrade is { } first && r.FinalGrade is { } last && last > first),
            overall.Percent, overall.Earned, overall.Possible, results);
    }
}
