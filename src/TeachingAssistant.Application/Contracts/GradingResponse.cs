using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Application.Contracts;

/// <summary>
/// One saved AI grading of a stored version (a draft for the teacher; never written to Classroom). Review is Markdown
/// (score per task and short feedback); Score, MaxScore and Bonus repeat it as numbers; Mistakes point at lines of the
/// code where they can. InstructionsVersion is the stored instructions version it was graded against. SentFiles lists
/// everything the model was given; SkippedFiles what was left out and why.
/// </summary>
public record GradingResponse(
    Guid Id,
    Guid VersionId,
    GradingStatus Status,
    string Model,
    string Review,
    double? Score,
    double? MaxScore,
    double? Bonus,
    IReadOnlyList<CodeMistake> Mistakes,
    int InstructionsVersion,
    IReadOnlyList<string> SentFiles,
    IReadOnlyList<string> SkippedFiles,
    DateTimeOffset CreatedAt);

/// <summary>
/// One mistake found by the model. File is a path from GET /versions/{id}/code, and StartLine/EndLine (1-based,
/// inclusive) are lines that exist in that file; both were checked against the stored code. File is null for a mistake
/// about the submission as a whole (for example a missing class), and the lines are null when the model's lines did not
/// fit the file.
/// </summary>
public record CodeMistake(
    string? File,
    int? StartLine,
    int? EndLine,
    MistakeSeverity Severity,
    MistakeCategory Category,
    string Message);

/// <summary>
/// One saved grading in a list of drafts: which assignment, student and version, the score and how many mistakes of each
/// severity. Current is true for the newest draft of its version (the one the workspace shows). StudentId is the
/// student's Google user ID.
/// </summary>
public record AssignmentGradingResponse(
    Guid Id,
    Guid VersionId,
    int VersionNumber,
    string AssignmentId,
    string SubmissionId,
    string StudentId,
    string StudentName,
    GradingStatus Status,
    bool Current,
    double? Score,
    double? MaxScore,
    double? Bonus,
    int Major,
    int Minor,
    int Style,
    string Model,
    DateTimeOffset CreatedAt);
