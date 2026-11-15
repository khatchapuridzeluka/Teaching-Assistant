using TeachingAssistant.Application.Contracts;

namespace TeachingAssistant.Application.Interfaces.Services;

public interface IGradingGuidanceService
{
    Task<GradingGuidanceResponse> GetAsync(string courseId, CancellationToken cancellationToken = default);
}
