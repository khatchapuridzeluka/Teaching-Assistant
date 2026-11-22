namespace TeachingAssistant.Application.Contracts;

/// <summary>
/// One model against the teacher's approved reports. Accuracy is 100 minus the average gap between its total and the
/// teacher's, in points per 100. Of the mistakes the teacher kept, Found is how many it found again (matched by file and
/// lines, or category for whole-submission mistakes) and FoundPercent their share; RepeatedFalseAlarms are mistakes it
/// flagged that the teacher had rejected; Unreviewed are mistakes it flagged that match nothing in the report, so nobody has
/// judged them. AverageSeconds is the time per grading, compile check included.
/// </summary>
public record ModelComparisonRow(
    string Model,
    int Graded,
    int ScoresCompared,
    double? Accuracy,
    int WithinTolerance,
    double? MeanDifference,
    int TeacherMistakes,
    int Found,
    double? FoundPercent,
    int RepeatedFalseAlarms,
    int Unreviewed,
    double? AverageSeconds,
    int Failed);
