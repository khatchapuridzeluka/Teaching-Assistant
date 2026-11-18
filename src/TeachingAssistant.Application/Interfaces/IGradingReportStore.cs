using TeachingAssistant.Application.Models;
using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Application.Interfaces;

public interface IGradingReportStore
{
    Task<GradingReport?> FindByGradingAsync(Guid gradingId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReportScore>> GetScoresAsync(IReadOnlyCollection<Guid> reportIds, CancellationToken cancellationToken = default);

    /// <summary>Every approved report with its AI draft, in one course or in all of them (courseId null).</summary>
    Task<IReadOnlyList<ApprovedReport>> GetApprovedAsync(string? courseId, CancellationToken cancellationToken = default);

    void Add(GradingReport report);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
