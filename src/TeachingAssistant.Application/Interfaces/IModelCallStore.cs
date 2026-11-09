using TeachingAssistant.Domain.Usage;

namespace TeachingAssistant.Application.Interfaces;

public interface IModelCallStore
{
    Task RecordAsync(ModelCall call, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ModelCall>> GetSinceAsync(DateTimeOffset since, CancellationToken cancellationToken = default);
}
