using TeachingAssistant.Application.Contracts;

namespace TeachingAssistant.Application.Interfaces.Services;

public interface IGradingExportService
{
    Task<ExportedFile?> ExportPdfAsync(Guid gradingId, CancellationToken cancellationToken = default);
}
