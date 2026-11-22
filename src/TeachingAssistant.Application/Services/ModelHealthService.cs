using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Exceptions;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Interfaces.Services;

namespace TeachingAssistant.Application.Services;

public class ModelHealthService : IModelHealthService
{
    private const string TestPrompt = "Reply with the single word: ok";

    private readonly ILanguageModel _languageModel;
    private readonly TimeProvider _timeProvider;

    public ModelHealthService(ILanguageModel languageModel, TimeProvider timeProvider)
    {
        _languageModel = languageModel;
        _timeProvider = timeProvider;
    }

    public async Task<ModelHealthResponse> CheckAsync(CancellationToken cancellationToken = default)
    {
        var start = _timeProvider.GetTimestamp();

        try
        {
            var reply = await _languageModel.CompleteAsync(TestPrompt, cancellationToken);
            return new ModelHealthResponse
            {
                Healthy = true,
                Model = reply.Model,
                LatencyMs = ElapsedMs(start),
                Reply = reply.Text,
            };
        }
        catch (LanguageModelUnavailableException ex)
        {
            return new ModelHealthResponse
            {
                Healthy = false,
                Model = _languageModel.ModelId,
                LatencyMs = ElapsedMs(start),
                Error = ex.Message,
            };
        }
    }

    private long ElapsedMs(long start) => (long)_timeProvider.GetElapsedTime(start).TotalMilliseconds;
}
