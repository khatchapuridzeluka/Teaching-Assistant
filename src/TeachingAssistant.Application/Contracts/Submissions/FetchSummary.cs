namespace TeachingAssistant.Application.Contracts;

public record FetchSummary(
    int Submissions,
    int NewSubmissions,
    int NewVersions,
    int Unchanged,
    int NotSubmitted,
    IReadOnlyList<FetchProblem> Problems,
    int? InstructionsVersion,
    bool NewInstructions);
