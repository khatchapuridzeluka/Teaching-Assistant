namespace TeachingAssistant.Application.Contracts;

/// <summary>
/// One model against the teacher's approved reports. Accuracy is 100 minus the average gap between its total and the
/// teacher's, in points per 100. Of the mistakes the teacher kept, Found is how many it found again (matched by file and
/// lines, or category for whole-submission mistakes) and FoundPercent their share; RepeatedFalseAlarms are mistakes it
/// flagged that the teacher had rejected; Unreviewed are mistakes it flagged that match nothing in the report, so nobody has
/// judged them. AverageSeconds is the time per grading, compile check included.
/// </summary>
public record ModelComparisonRow
{
    public required string Model { get; init; }
    public required int Graded { get; init; }
    public required int ScoresCompared { get; init; }
    public double? Accuracy { get; init; }
    public required int WithinTolerance { get; init; }
    public double? MeanDifference { get; init; }
    public required int TeacherMistakes { get; init; }
    public required int Found { get; init; }
    public double? FoundPercent { get; init; }
    public required int RepeatedFalseAlarms { get; init; }
    public required int Unreviewed { get; init; }
    public double? AverageSeconds { get; init; }
    public required int Failed { get; init; }
}
