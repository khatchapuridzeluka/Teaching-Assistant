using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Interfaces.Services;
using TeachingAssistant.Domain.Ingestion;
using TeachingAssistant.Domain.Profiling;

namespace TeachingAssistant.Application.Services;

/// <summary>
/// Copies a whole course from the source in a few requests and without downloading files: the roster (students and
/// enrollments), the assignments (maximum points and deadlines) and every student's state and grade history for every
/// assignment. Profiles and scores then read our database only. The source stays the owner: grades are only read.
/// Students who left the roster are kept, inactive, so their past work stays readable.
/// </summary>
public class CourseSyncService : ICourseSyncService
{
    private readonly ISubmissionSource _source;
    private readonly IStudentStore _students;
    private readonly ICourseAssignmentStore _assignments;
    private readonly ISubmissionStore _submissions;
    private readonly ISnapshotStorage _storage;
    private readonly TimeProvider _timeProvider;

    public CourseSyncService(
        ISubmissionSource source,
        IStudentStore students,
        ICourseAssignmentStore assignments,
        ISubmissionStore submissions,
        ISnapshotStorage storage,
        TimeProvider timeProvider)
    {
        _source = source;
        _students = students;
        _assignments = assignments;
        _submissions = submissions;
        _storage = storage;
        _timeProvider = timeProvider;
    }

    public async Task<CourseSyncSummary> SyncAsync(string courseId, CancellationToken cancellationToken = default)
    {
        // Four independent reads, all at the same time.
        var courseCall = _source.GetCourseAsync(courseId, cancellationToken);
        var assignmentsCall = _source.GetAssignmentsAsync(courseId, cancellationToken);
        var rosterCall = _source.GetStudentsAsync(courseId, cancellationToken);
        var submissionsCall = _source.GetCourseSubmissionsAsync(courseId, cancellationToken);
        await Task.WhenAll(courseCall, assignmentsCall, rosterCall, submissionsCall);
        var (course, assignments, roster, listed) = (await courseCall, await assignmentsCall, await rosterCall, await submissionsCall);
        var now = _timeProvider.GetUtcNow();

        // The database is read and written one step at a time: the stores share one database context.
        await SyncAssignmentsAsync(courseId, assignments, now, cancellationToken);
        var (newStudents, leftStudents) = await SyncRosterAsync(courseId, roster, now, cancellationToken);

        var titles = assignments.ToDictionary(a => a.Id, a => a.Title);
        var stored = (await _submissions.GetByCourseAsync(SubmissionSource.GoogleClassroom, courseId, cancellationToken))
            .ToDictionary(s => s.ExternalSubmissionId);
        int newSubmissions = 0, graded = 0, checkedCount = 0;

        foreach (var item in listed)
        {
            // Work for an assignment the list does not show (e.g. a draft the teacher has not published) is skipped.
            if (!titles.TryGetValue(item.AssignmentId, out var title))
            {
                continue;
            }

            checkedCount++;
            if (!stored.TryGetValue(item.Id, out var submission))
            {
                // No files yet: the folder is only chosen here, a fetch writes into it later.
                submission = new Submission(SubmissionSource.GoogleClassroom, courseId, item.AssignmentId, item.StudentId,
                    item.StudentName, item.Id, _storage.GetSubmissionFolder(course.Name, title, item.StudentName, item.StudentId), now);
                _submissions.Add(submission);
                stored[item.Id] = submission;
                newSubmissions++;
            }

            SubmissionStatus.Record(submission, item, now);
            if (submission.FinalGrade is not null)
            {
                graded++;
            }
        }

        await _submissions.SaveChangesAsync(cancellationToken);
        return new CourseSyncSummary(roster.Count, newStudents, leftStudents, assignments.Count, checkedCount, newSubmissions, graded);
    }

    private async Task SyncAssignmentsAsync(
        string courseId, IReadOnlyList<AssignmentResponse> assignments, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var stored = (await _assignments.GetByCourseAsync(SubmissionSource.GoogleClassroom, courseId, cancellationToken))
            .ToDictionary(a => a.ExternalAssignmentId);

        foreach (var assignment in assignments)
        {
            if (stored.TryGetValue(assignment.Id, out var existing))
            {
                existing.Update(assignment.Title, assignment.MaxPoints, assignment.DueAt, now);
            }
            else
            {
                _assignments.Add(new CourseAssignment(SubmissionSource.GoogleClassroom, courseId, assignment.Id,
                    assignment.Title, assignment.MaxPoints, assignment.DueAt, now));
            }
        }
    }

    // Returns how many students are new and how many left the roster.
    private async Task<(int New, int Left)> SyncRosterAsync(
        string courseId, IReadOnlyList<Models.RosterStudent> roster, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var ids = roster.Select(s => s.Id).ToList();
        var known = (await _students.FindByExternalIdsAsync(SubmissionSource.GoogleClassroom, ids, cancellationToken))
            .ToDictionary(s => s.ExternalUserId);
        var created = 0;

        foreach (var entry in roster)
        {
            if (!known.TryGetValue(entry.Id, out var student))
            {
                student = new Student(SubmissionSource.GoogleClassroom, entry.Id, entry.Name, now);
                _students.Add(student);
                known[entry.Id] = student;
                created++;
            }

            student.Rename(entry.Name);
            student.EnrollIn(courseId, now);
        }

        var onRoster = ids.ToHashSet();
        var left = (await _students.GetEnrolledAsync(SubmissionSource.GoogleClassroom, courseId, cancellationToken))
            .Where(s => !onRoster.Contains(s.ExternalUserId))
            .ToList();
        foreach (var student in left)
        {
            student.LeaveCourse(courseId);
        }

        return (created, left.Count);
    }
}
