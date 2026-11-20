using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Application.Models;

namespace TeachingAssistant.Application.Interfaces;

public interface ILanguageModel
{
    string ModelId { get; }

    string GradingModelId { get; }

    Task<LanguageModelReply> CompleteAsync(string prompt, CancellationToken cancellationToken = default);

    Task<LanguageModelReply> ChatAsync(
        string systemPrompt, IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default);

    Task<AgentReply> RunAgentAsync(
        string systemPrompt, IReadOnlyList<AgentEntry> history, IReadOnlyList<object> tools, AgentRunHooks? hooks = null,
        CancellationToken cancellationToken = default);

    /// <summary>Grades with the configured grading model, or with <paramref name="model"/> when given (evaluation runs).</summary>
    Task<LanguageModelReply> GradeAsync(
        string systemPrompt, IReadOnlyList<PromptPart> parts, string outputSchema, string? model = null,
        CancellationToken cancellationToken = default);

    Task<LanguageModelReply> AnalyzeAsync(
        string systemPrompt, IReadOnlyList<PromptPart> parts, string outputSchema, CancellationToken cancellationToken = default);
}
