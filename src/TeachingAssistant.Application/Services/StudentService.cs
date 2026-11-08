using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Interfaces.Services;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Domain.Grading;
using TeachingAssistant.Domain.Ingestion;
using TeachingAssistant.Domain.Profiling;

namespace TeachingAssistant.Application.Services;

public class StudentService : IStudentService
{
    private readonly IStudentStore _students;
    private readonly ICourseAssignmentStore _assignments;
    private readonly ISubmissionStore _submissions;
    private readonly IStudentPhotoStorage _photos;
    private readonly TimeProvider _timeProvider;

    public StudentService(
        IStudentStore students, ICourseAssignmentStore assignments, ISubmissionStore submissions, IStudentPhotoStorage photos,
        TimeProvider timeProvider)
    {
        _students = students;
        _assignments = assignments;
        _submissions = submissions;
        _photos = photos;
        _timeProvider = timeProvider;
    }

    public async Task<StudentPhoto?> OpenPhotoAsync(string externalUserId, CancellationToken cancellationToken = default)
    {
        var student = (await _students.FindByExternalIdsAsync(SubmissionSource.GoogleClassroom, [externalUserId], cancellationToken))
            .FirstOrDefault();
        if (student is not { PhotoStoredName: { } storedName, PhotoHash: { } hash })
        {
            return null;
        }

        var stream = _photos.Open(storedName);
        return stream is null ? null : new StudentPhoto(stream, student.PhotoContentType ?? "image/jpeg", hash);
    }

    public async Task<CourseStudentsResponse> GetCourseStudentsAsync(string courseId, CancellationToken cancellationToken = default)
    {
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
