namespace TeachingAssistant.Application.Contracts;

public record CourseSyncSummary(
    int Students,
    int NewStudents,
    int LeftStudents,
    int Assignments,
    int Submissions,
    int NewSubmissions,
    int Graded,
    int PhotosUpdated,
    int PhotosFailed);
