using TeachingAssistant.Application.Contracts;

namespace TeachingAssistant.Application.Interfaces.Services;

public interface IGradingReportService
{
    Task<GradingReportResponse?> GetAsync(Guid gradingId, CancellationToken cancellationToken = default);

    Task<GradingReportResponse?> SaveAsync(Guid gradingId, SaveGradingReportRequest request, CancellationToken cancellationToken = default);

    Task<GradingReportResponse?> ApproveAsync(Guid gradingId, SaveGradingReportRequest request, CancellationToken cancellationToken = default);
}
