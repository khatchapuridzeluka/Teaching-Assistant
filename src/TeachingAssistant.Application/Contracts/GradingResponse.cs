using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Application.Contracts;

/// <summary>
/// The model's draft review of one stored version: Review is Markdown (score per task and short feedback), Mistakes are
/// the individual mistakes, each pointing at lines of the code where it can. A draft for the teacher, not saved yet.
/// InstructionsVersion is the stored version of the assignment's instructions it was graded against. SentFiles lists
/// everything the model was given (instructions and student files); SkippedFiles lists what was left out and why, so the
/// teacher can judge whether the review saw the whole submission.
/// </summary>
public record GradingResponse(
    Guid VersionId,
    string Model,
    string Review,
    IReadOnlyList<CodeMistake> Mistakes,
    int InstructionsVersion,
    IReadOnlyList<string> SentFiles,
    IReadOnlyList<string> SkippedFiles);

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
