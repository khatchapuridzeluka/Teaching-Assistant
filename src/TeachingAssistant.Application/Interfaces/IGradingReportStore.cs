using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Application.Interfaces;

public interface IGradingReportStore
{
    Task<GradingReport?> FindByGradingAsync(Guid gradingId, CancellationToken cancellationToken = default);

    void Add(GradingReport report);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
