using TeachingAssistant.Application.Contracts;

namespace TeachingAssistant.Application.Interfaces.Services;

/// <summary>
/// Grades stored submissions with the language model against the assignment's instructions. Reads the code only;
/// nothing is compiled or run yet.
/// </summary>
public interface IGradingService
{
    /// <summary>
    /// Sends the version's files and the assignment's instructions to the model and returns its draft review, or null
    /// when the version does not exist. Throws <see cref="Exceptions.InvalidRequestException"/> when there is nothing to
    /// grade or no instructions, and <see cref="Exceptions.LanguageModelUnavailableException"/> when the model fails.
    /// </summary>
    Task<GradingResponse?> GradeVersionAsync(Guid versionId, CancellationToken cancellationToken = default);
}
