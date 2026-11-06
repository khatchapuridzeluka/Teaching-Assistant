namespace TeachingAssistant.Application.Contracts;

/// <summary>
/// What one fetch of an assignment did. Unchanged submissions were already stored and were not downloaded again.
/// NotSubmitted counts students who have not turned anything in. Problems lists what needs the teacher's attention.
/// InstructionsVersion is the stored version of the assignment's instructions used for grading (null when none could be
/// stored); NewInstructions is true when this fetch stored it because the instructions changed (or were stored for the first time).
/// </summary>
public record FetchSummary(
    int Submissions,
    int NewSubmissions,
    int NewVersions,
    int Unchanged,
    int NotSubmitted,
    IReadOnlyList<FetchProblem> Problems,
    int? InstructionsVersion,
    bool NewInstructions);

/// <summary>
/// Something a fetch could not handle for one student, e.g. an unsupported attachment or a failed download. For a
/// problem with the assignment's instructions, StudentName is "Instructions".
/// </summary>
public record FetchProblem(string StudentName, string Detail);
