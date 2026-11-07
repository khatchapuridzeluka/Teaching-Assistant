namespace TeachingAssistant.Application.Contracts;

/// <summary>
/// What syncing a course did: roster, assignments, and every student's state and grade per assignment (no files).
/// </summary>
/// <param name="Students">Students on the roster now.</param>
/// <param name="NewStudents">Students seen for the first time.</param>
/// <param name="LeftStudents">Students no longer on the roster (kept, marked inactive).</param>
/// <param name="Assignments">Assignments in the course.</param>
/// <param name="Submissions">Submissions checked (every student × assignment the source lists).</param>
/// <param name="NewSubmissions">Submissions recorded for the first time.</param>
/// <param name="Graded">Submissions that have a grade.</param>
public record CourseSyncSummary(
    int Students,
    int NewStudents,
    int LeftStudents,
    int Assignments,
    int Submissions,
    int NewSubmissions,
    int Graded);
