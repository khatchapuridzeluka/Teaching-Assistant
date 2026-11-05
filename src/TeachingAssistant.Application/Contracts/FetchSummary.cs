namespace TeachingAssistant.Application.Contracts;

/// <summary>
/// What one fetch of an assignment did. Unchanged submissions were already stored and were not downloaded again.
/// NotSubmitted counts students who have not turned anything in. Problems lists what needs the teacher's attention.
/// </summary>
public record FetchSummary(
    int Submissions,
    int NewSubmissions,
    int NewVersions,
    int Unchanged,
    int NotSubmitted,
    IReadOnlyList<FetchProblem> Problems);

/// <summary>
/// Something a fetch could not handle for one student, e.g. an unsupported attachment or a failed download.
/// </summary>
public record FetchProblem(string StudentName, string Detail);
