namespace TeachingAssistant.Application.Interfaces.Services;

public interface ITestCodeService
{
    Task GenerateAsync(Guid testSetId, CancellationToken cancellationToken = default);
}
