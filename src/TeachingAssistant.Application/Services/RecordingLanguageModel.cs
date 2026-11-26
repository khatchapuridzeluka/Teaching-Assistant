using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Domain.Usage;

namespace TeachingAssistant.Application.Services;

public class RecordingLanguageModel : ILanguageModel
{
    private readonly ILanguageModel _inner;
    private readonly IModelCallStore _calls;
    private readonly ModelCallContext _context;
    private readonly TimeProvider _timeProvider;

    public RecordingLanguageModel(ILanguageModel inner, IModelCallStore calls, ModelCallContext context, TimeProvider timeProvider)
    {
        _inner = inner;
        _calls = calls;
        _context = context;
        _timeProvider = timeProvider;
    }

    public string ModelId => _inner.ModelId;

    public Task<LanguageModelReply> CompleteAsync(string prompt, CancellationToken cancellationToken = default)
    {
        return RecordAsync(ModelCallPurpose.HealthCheck, _inner.ModelId, () => _inner.CompleteAsync(prompt, cancellationToken));
    }

    public Task<LanguageModelReply> ChatAsync(
        string systemPrompt, IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default)
    {
        return RecordAsync(ModelCallPurpose.Chat, _inner.ModelId, () => _inner.ChatAsync(systemPrompt, messages, cancellationToken));
    }

    public async Task<AgentReply> RunAgentAsync(
        string systemPrompt, IReadOnlyList<AgentEntry> history, IReadOnlyList<object> tools, AgentRunHooks? hooks = null,
        CancellationToken cancellationToken = default)
    {
        AgentReply? agentReply = null;
        await RecordAsync(ModelCallPurpose.Chat, _inner.ModelId, async () =>
        {
            agentReply = await _inner.RunAgentAsync(systemPrompt, history, tools, hooks, cancellationToken);
            return new LanguageModelReply { Model = agentReply.Model, Text = agentReply.Text, Usage = agentReply.Usage };
        });
        return agentReply!;
    }

    public Task<LanguageModelReply> GradeAsync(
        IReadOnlyList<TextPart> system, IReadOnlyList<PromptPart> parts, string outputSchema, string? model = null,
        CancellationToken cancellationToken = default)
    {
        var purpose = _context.Evaluation ? ModelCallPurpose.Evaluation
            : _context.TryOut ? ModelCallPurpose.Try
            : _context.PartOfGradeAll ? ModelCallPurpose.GradeAll : ModelCallPurpose.Grading;
        var requested = model ?? _context.GradingModel;
        return RecordAsync(purpose, requested ?? _inner.GradingModelId,
            () => _inner.GradeAsync(system, parts, outputSchema, requested, cancellationToken));
    }

    public Task<LanguageModelReply> AnalyzeAsync(
        string systemPrompt, IReadOnlyList<PromptPart> parts, string outputSchema, CancellationToken cancellationToken = default)
    {
        var purpose = _context.StudentAnalysis ? ModelCallPurpose.StudentAnalysis : ModelCallPurpose.MistakeAnalysis;
        return RecordAsync(purpose, _inner.GradingModelId,
            () => _inner.AnalyzeAsync(systemPrompt, parts, outputSchema, cancellationToken));
    }

    public Task<LanguageModelReply> DesignTestsAsync(
        string systemPrompt, IReadOnlyList<PromptPart> parts, string outputSchema, CancellationToken cancellationToken = default)
    {
        return RecordAsync(ModelCallPurpose.TestDesign, _inner.GradingModelId,
            () => _inner.DesignTestsAsync(systemPrompt, parts, outputSchema, cancellationToken));
    }

    public Task<LanguageModelReply> WriteAdapterAsync(
        string systemPrompt, IReadOnlyList<PromptPart> parts, string outputSchema, CancellationToken cancellationToken = default)
    {
        return RecordAsync(ModelCallPurpose.TestAdapter, _inner.AdapterModelId,
            () => _inner.WriteAdapterAsync(systemPrompt, parts, outputSchema, cancellationToken));
    }

    public string GradingModelId => _inner.GradingModelId;

    public string AdapterModelId => _inner.AdapterModelId;

    private async Task<LanguageModelReply> RecordAsync(ModelCallPurpose purpose, string requestedModel, Func<Task<LanguageModelReply>> call)
    {
        var startedAt = _timeProvider.GetUtcNow();
        var started = _timeProvider.GetTimestamp();
        try
        {
            var reply = await call();
            var tokens = reply.Usage is { } usage
                ? new ModelCallTokens
                {
                    Input = usage.InputTokens,
                    Output = usage.OutputTokens,
                    CacheRead = usage.CacheReadTokens,
                    CacheWrite = usage.CacheWriteTokens,
                }
                : null;
            await _calls.RecordAsync(
                ModelCall.Success(purpose, requestedModel, reply.Model, tokens, startedAt, _timeProvider.GetElapsedTime(started), _context.Subject),
                CancellationToken.None);
            return reply;
        }
        catch (Exception ex)
        {
            var error = ex is OperationCanceledException ? "Canceled before the model answered." : ex.Message;
            await _calls.RecordAsync(
                ModelCall.Failure(purpose, requestedModel, error, startedAt, _timeProvider.GetElapsedTime(started), _context.Subject),
                CancellationToken.None);
            throw;
        }
    }
}
