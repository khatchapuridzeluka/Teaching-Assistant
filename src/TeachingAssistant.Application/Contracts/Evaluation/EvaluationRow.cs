namespace TeachingAssistant.Application.Contracts;

/// <summary>
/// One compared report. Scores are totals (score plus bonus); AiPercent and TeacherPercent are those totals in percent of the
/// maximum, and Difference is the AI's minus the teacher's in percent of the maximum (positive: the AI was more lenient), null
/// when either total is missing. ClassroomGrade is the grade returned in Classroom for the same submission, if any.
/// </summary>
public record EvaluationRow(
    Guid ReportId,
    Guid GradingId,
    string CourseId,
    string AssignmentId,
    string? AssignmentTitle,
    string StudentId,
    string? StudentName,
    string Model,
    double? AiScore,
    double? TeacherScore,
    double? MaxScore,
    double? AiPercent,
    double? TeacherPercent,
    double? Difference,
    int Mistakes,
    int MistakesEdited,
    int MistakesRejected,
    double? ClassroomGrade,
    DateTimeOffset? ApprovedAt);
