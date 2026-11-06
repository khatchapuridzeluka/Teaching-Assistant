namespace TeachingAssistant.Application.Contracts;

/// <summary>
/// The model's draft review of one stored version (Markdown: score per task, mistakes, short feedback). A draft for the
/// teacher, not saved yet. InstructionsVersion is the stored version of the assignment's instructions it was graded
/// against. SentFiles lists everything the model was given (instructions and student files); SkippedFiles lists what was
/// left out and why, so the teacher can judge whether the review saw the whole submission.
/// </summary>
public record GradingResponse(
    Guid VersionId,
    string Model,
    string Review,
    int InstructionsVersion,
    IReadOnlyList<string> SentFiles,
    IReadOnlyList<string> SkippedFiles);
