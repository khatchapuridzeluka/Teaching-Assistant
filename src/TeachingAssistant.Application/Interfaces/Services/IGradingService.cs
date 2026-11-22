using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Models;

namespace TeachingAssistant.Application.Interfaces.Services;

public interface IGradingService
{
    /// <summary>The models a version can be tried with, and the one that grades drafts now.</summary>
    TeachingAssistant.Application.Contracts.GradingModelsResponse GetModels();

    /// <summary>Grades a version the same way as a draft would be, with the given model, and returns it without saving it.</summary>
    Task<TeachingAssistant.Application.Contracts.GradingResponse?> TryModelAsync(
        Guid versionId, string model, CancellationToken cancellationToken = default);

    /// <summary>Grades a version the same way as a draft would be, with the given model, without saving anything (evaluation runs).</summary>
    Task<TeachingAssistant.Application.Models.EvaluationGrading?> GradeForEvaluationAsync(
        Guid versionId, string model, CancellationToken cancellationToken = default);

    Task<GradingResponse?> GradeVersionAsync(Guid versionId, CancellationToken cancellationToken = default);

    Task<GradableVersion?> CheckAsync(Guid versionId, CancellationToken cancellationToken = default);

    Task<GradeAllPlan> PlanGradeAllAsync(
        string courseId, string assignmentId, bool regrade = false, CancellationToken cancellationToken = default);

    Task<GradeAllPlan> PlanGradeStudentAsync(string courseId, string studentId, bool regrade = false, CancellationToken cancellationToken = default);

    Task<string?> FindStudentNameAsync(string courseId, string studentId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PromptPart>?> BuildInstructionsAsync(string assignmentId, CancellationToken cancellationToken = default);
}
