using TeachingAssistant.Application.Contracts;

namespace TeachingAssistant.Application.Interfaces.Services;

public interface IGradebookService
{
    Task<GradebookResponse> GetAsync(string courseId, CancellationToken cancellationToken = default);

    Task<byte[]> ExportCsvAsync(string courseId, CancellationToken cancellationToken = default);
}
