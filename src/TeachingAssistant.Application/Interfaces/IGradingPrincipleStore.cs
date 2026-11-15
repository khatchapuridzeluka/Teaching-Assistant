using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Application.Interfaces;

public interface IGradingPrincipleStore
{
    /// <summary>The principles not deleted, in the order the grader reads them.</summary>
    Task<IReadOnlyList<GradingPrinciple>> GetActiveAsync(CancellationToken cancellationToken = default);

    Task<GradingPrinciple?> FindAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>The highest position used so far, deleted principles included (0 when there are none).</summary>
    Task<int> GetLastPositionAsync(CancellationToken cancellationToken = default);

    void Add(GradingPrinciple principle);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
