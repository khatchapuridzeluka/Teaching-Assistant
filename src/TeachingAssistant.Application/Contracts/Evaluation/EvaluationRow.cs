namespace TeachingAssistant.Application.Contracts;

/// <summary>
/// One compared report. Scores are totals (score plus bonus); AiPercent and TeacherPercent are those totals in percent of the
/// maximum, and Difference is the AI's minus the teacher's in percent of the maximum (positive: the AI was more lenient), null
/// when either total is missing. ClassroomGrade is the grade returned in Classroom for the same submission, if any.
/// </summary>
public record EvaluationRow
{
    public required Guid ReportId { get; init; }
    public required Guid GradingId { get; init; }
    public required string CourseId { get; init; }
    public required string AssignmentId { get; init; }
    public string? AssignmentTitle { get; init; }
    public required string StudentId { get; init; }
    public string? StudentName { get; init; }
    public required string Model { get; init; }
    public double? AiScore { get; init; }
    public double? TeacherScore { get; init; }
    public double? MaxScore { get; init; }
    public double? AiPercent { get; init; }
    public double? TeacherPercent { get; init; }
    public double? Difference { get; init; }
    public required int Mistakes { get; init; }
    public required int MistakesEdited { get; init; }
    public required int MistakesRejected { get; init; }
    public double? ClassroomGrade { get; init; }
    public DateTimeOffset? ApprovedAt { get; init; }
}
