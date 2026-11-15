using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Application.Interfaces;

public interface IPolicyRuleStore
{
    /// <summary>The rules not deleted, in the order the grader reads them.</summary>
    Task<IReadOnlyList<PolicyRule>> GetActiveAsync(CancellationToken cancellationToken = default);

    Task<PolicyRule?> FindAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>The highest position used so far, deleted rules included (0 when there are none).</summary>
    Task<int> GetLastPositionAsync(CancellationToken cancellationToken = default);

    void Add(PolicyRule rule);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
