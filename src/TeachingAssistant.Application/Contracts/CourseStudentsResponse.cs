namespace TeachingAssistant.Application.Contracts;

/// <summary>
/// A course's students with their results per assignment and overall, from our database (filled by a course sync).
/// </summary>
/// <param name="SyncedAt">When the course was last synced; null before the first sync.</param>
/// <param name="Assignments">The course's assignments, by deadline.</param>
/// <param name="Students">Students on the roster first, then those who left; by name.</param>
public record CourseStudentsResponse(
    DateTimeOffset? SyncedAt,
    IReadOnlyList<StudentAssignmentColumn> Assignments,
    IReadOnlyList<CourseStudentResponse> Students);

/// <summary>One assignment as a column of the students table.</summary>
public record StudentAssignmentColumn(string Id, string Title, double? MaxPoints, DateTimeOffset? DueAt);

/// <summary>
/// One student of a course. Overall is final points over possible points; missing work counts as 0 once past its
/// deadline, work waiting for a grade and work not due yet are left out.
/// </summary>
/// <param name="Id">Our student ID.</param>
/// <param name="ExternalUserId">The student's Google user ID.</param>
/// <param name="Name">Name as Classroom shows it.</param>
/// <param name="Active">On the course's roster as of the last sync.</param>
/// <param name="TurnedIn">Assignments handed in (turned in or returned).</param>
/// <param name="Missing">Assignments not handed in and past their deadline.</param>
/// <param name="Late">Assignments handed in late.</param>
/// <param name="Improved">Assignments whose final grade is higher than the first one returned (fixed after a resubmission).</param>
/// <param name="OverallPercent">Null when nothing counts yet.</param>
/// <param name="Earned">Final points earned over the counted assignments.</param>
/// <param name="Possible">Maximum points of the counted assignments.</param>
/// <param name="Results">One per assignment, in the order of the assignments.</param>
public record CourseStudentResponse(
    Guid Id,
    string ExternalUserId,
    string Name,
    bool Active,
    int TurnedIn,
    int Missing,
    int Late,
    int Improved,
    double? OverallPercent,
    double Earned,
    double Possible,
    IReadOnlyList<StudentAssignmentResult> Results);

/// <summary>A student's result for one assignment; grades come from Classroom (initial = first returned, final = latest).</summary>
/// <param name="AssignmentId">Classroom's assignment ID.</param>
/// <param name="SubmissionId">Classroom's submission ID (opens the student in the assignment workspace); null when there is none.</param>
/// <param name="State">Classroom's state, e.g. CREATED, TURNED_IN, RETURNED; null when there is no submission.</param>
/// <param name="TurnedIn">Handed in (turned in or returned).</param>
/// <param name="Late">Handed in after the deadline.</param>
/// <param name="Missing">Not handed in and past the deadline: counts as 0.</param>
/// <param name="InitialGrade">The first grade returned.</param>
/// <param name="FinalGrade">The latest grade returned.</param>
/// <param name="Returns">Every grade the teacher returned, oldest first.</param>
public record StudentAssignmentResult(
    string AssignmentId,
    string? SubmissionId,
    string? State,
    bool TurnedIn,
    bool Late,
    bool Missing,
    double? InitialGrade,
    double? FinalGrade,
    IReadOnlyList<GradeReturn> Returns);

/// <summary>One grade the teacher returned to the student in Classroom.</summary>
public record GradeReturn(double? Points, DateTimeOffset At);
