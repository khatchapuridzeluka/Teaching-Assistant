namespace TeachingAssistant.Application.Contracts;

/// <summary>The evaluation numbers for the drafts one model wrote, with how many of its drafts were discarded as poor grading.</summary>
public record EvaluationModel(
    string Model,
    int Reports,
    int ScoresCompared,
    double? Accuracy,
    int WithinTolerance,
    double? MeanAbsoluteDifference,
    int Mistakes,
    int MistakesRejected,
    int DiscardedPoorGrading);
