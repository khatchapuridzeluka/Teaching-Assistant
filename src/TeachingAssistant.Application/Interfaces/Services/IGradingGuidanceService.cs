using TeachingAssistant.Application.Contracts;

namespace TeachingAssistant.Application.Interfaces.Services;

public interface IGradingGuidanceService
{
    Task<GradingGuidanceResponse> GetAsync(string courseId, CancellationToken cancellationToken = default);

    /// <summary>Sets the course's own guidance; an empty text removes it.</summary>
    Task<GradingGuidanceResponse> SetCourseGuidanceAsync(
        string courseId, CourseGuidanceRequest request, CancellationToken cancellationToken = default);

    /// <summary>Adds a principle after the existing ones.</summary>
    Task<GradingPrincipleResponse> AddPrincipleAsync(GradingPrincipleRequest request, CancellationToken cancellationToken = default);

    /// <summary>Changes a principle's title and text; null when it does not exist or is deleted.</summary>
    Task<GradingPrincipleResponse?> UpdatePrincipleAsync(
        Guid id, GradingPrincipleRequest request, CancellationToken cancellationToken = default);

    /// <summary>Marks a principle deleted; false when it does not exist or is already deleted.</summary>
    Task<bool> DeletePrincipleAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Adds a rule after the existing ones.</summary>
    Task<PolicyRuleResponse> AddRuleAsync(PolicyRuleRequest request, CancellationToken cancellationToken = default);

    /// <summary>Changes a rule; null when it does not exist or is deleted.</summary>
    Task<PolicyRuleResponse?> UpdateRuleAsync(Guid id, PolicyRuleRequest request, CancellationToken cancellationToken = default);

    /// <summary>Marks a rule deleted; false when it does not exist or is already deleted.</summary>
    Task<bool> DeleteRuleAsync(Guid id, CancellationToken cancellationToken = default);
}
