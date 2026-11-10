using System.Reflection;
using Anthropic;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Application.Tools;
using Contracts = TeachingAssistant.Application.Contracts;

namespace TeachingAssistant.Infrastructure.LanguageModel;

internal static class ClaudeAgent
{
    public static async Task<LanguageModelReply> RunAsync(
        AnthropicClient client, string model, int maxTokens, string systemPrompt,
        IReadOnlyList<Contracts.ChatMessage> messages, IReadOnlyList<object> tools, CancellationToken cancellationToken)
    {
        var agent = new ChatClientAgent(
            client.AsIChatClient(model, maxTokens),
            instructions: systemPrompt,
            name: "teaching-assistant",
            tools: tools.SelectMany(ToFunctions).ToList());

        var response = await agent.RunAsync(messages.Select(ToMessage).ToList(), cancellationToken: cancellationToken);
        return new LanguageModelReply(model, response.Text.Trim(), ToUsage(response.Usage));
    }

    private static IEnumerable<AITool> ToFunctions(object tools)
    {
        foreach (var method in tools.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance))
        {
            if (method.GetCustomAttribute<AgentToolAttribute>() is { } tool)
            {
                yield return AIFunctionFactory.Create(method, tools, tool.Name);
            }
        }
    }

    private static ChatMessage ToMessage(Contracts.ChatMessage message)
    {
        return new ChatMessage(message.Role == Contracts.ChatRole.User ? ChatRole.User : ChatRole.Assistant, message.Text);
    }

    private static LanguageModelUsage? ToUsage(UsageDetails? usage)
    {
        return usage is null
            ? null
            : new LanguageModelUsage(usage.InputTokenCount ?? 0, usage.OutputTokenCount ?? 0, usage.CachedInputTokenCount ?? 0, 0);
    }
}
