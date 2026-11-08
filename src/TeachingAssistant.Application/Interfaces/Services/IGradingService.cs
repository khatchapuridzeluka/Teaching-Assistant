using TeachingAssistant.Application.Contracts;

namespace TeachingAssistant.Application.Interfaces.Services;

/// <summary>
/// Grades stored submissions with the language model against the assignment's instructions. Reads the code only;
/// nothing is compiled or run yet. Every grading is saved as a draft.
/// </summary>
public interface IGradingService
{
    /// <summary>
    /// Sends the version's files and the assignment's instructions to the model, saves its answer as a new draft and
    /// returns it, or null when the version does not exist. Throws <see cref="Exceptions.InvalidRequestException"/> when
    /// there is nothing to grade or no instructions, and <see cref="Exceptions.LanguageModelUnavailableException"/> when
    /// the model fails (then nothing is saved).
    /// </summary>
    Task<GradingResponse?> GradeVersionAsync(Guid versionId, CancellationToken cancellationToken = default);
}

/// <summary>The saved AI gradings (drafts): reading them back and discarding one. No model calls.</summary>
public interface IGradingDraftService
{
    /// <summary>The newest draft of a version (discarded ones are skipped); null when it has none.</summary>
    Task<GradingResponse?> GetCurrentAsync(Guid versionId, CancellationToken cancellationToken = default);

    /// <summary>Every grading of a version, drafts and discarded, newest first.</summary>
    Task<IReadOnlyList<GradingResponse>> GetHistoryAsync(Guid versionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The saved gradings of a course, newest first; with assignmentId only that assignment's, with studentId (Google user
    /// ID) only that student's. By default only the current draft of each version; with includeAll also earlier drafts and
    /// discarded ones.
    /// </summary>
    Task<IReadOnlyList<AssignmentGradingResponse>> GetByCourseAsync(
        string courseId, string? assignmentId, string? studentId, bool includeAll, CancellationToken cancellationToken = default);

    /// <summary>Discards a draft (it stays in the history) and returns it; null when there is no such grading.</summary>
    Task<GradingResponse?> DiscardAsync(Guid gradingId, CancellationToken cancellationToken = default);
}
