namespace TeachingAssistant.Application.Contracts;

/// <summary>The evaluation numbers for the drafts one model wrote, with how many of its drafts were discarded as poor grading.</summary>
public record EvaluationModel
{
    public required string Model { get; init; }
    public required int Reports { get; init; }
    public required int ScoresCompared { get; init; }
    public double? Accuracy { get; init; }
    public required int WithinTolerance { get; init; }
    public double? MeanAbsoluteDifference { get; init; }
    public required int Mistakes { get; init; }
    public required int MistakesRejected { get; init; }
    public required int DiscardedPoorGrading { get; init; }
}
