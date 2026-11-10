using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Application.Contracts;

public record CommonMistakesResponse(
    Guid AnalysisId,
    DateTimeOffset AnalyzedAt,
    string Model,
    string Summary,
    string NextLecture,
    int Students,
    int Mistakes,
    int Major,
    int Minor,
    int Style,
    IReadOnlyList<MistakeClusterResponse> Groups);

public record MistakeClusterResponse(
    Guid Id,
    string Title,
    string Explanation,
    string? ApprovedExplanation,
    DateTimeOffset? ApprovedAt,
    bool Ungrouped,
    int Students,
    double StudentPercent,
    int Count,
    double PointsLost,
    double? AveragePoints,
    int Major,
    int Minor,
    int Style,
    IReadOnlyList<ClusterMemberResponse> Members);

public record ClusterMemberResponse(
    Guid Id,
    Guid GradingMistakeId,
    Guid VersionId,
    string StudentId,
    string StudentName,
    string SubmissionId,
    MistakeSeverity Severity,
    MistakeCategory Category,
    double? Points,
    string Message,
    string? File,
    int? StartLine,
    int? EndLine);

public record ApproveExplanationRequest(string Explanation);
