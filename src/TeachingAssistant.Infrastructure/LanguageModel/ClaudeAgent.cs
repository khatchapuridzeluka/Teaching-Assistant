using System.Reflection;
using System.Text.Json;
using Anthropic;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Application.Tools;
using TeachingAssistant.Domain.Agents;

namespace TeachingAssistant.Infrastructure.LanguageModel;

internal static class ClaudeAgent
{
    public static IChatClient ChatClient(AnthropicClient client, string model, int maxTokens)
    {
        return client.AsIChatClient(model, maxTokens);
    }

    public static async Task<AgentReply> RunAsync(
        IChatClient chatClient, string model, string systemPrompt, IReadOnlyList<AgentEntry> history, IReadOnlyList<object> tools,
        CancellationToken cancellationToken)
    {
        var agent = new ChatClientAgent(
            chatClient,
            instructions: systemPrompt,
            name: "teaching-assistant",
            tools: tools.SelectMany(ToFunctions).ToList());

        var response = await agent.RunAsync(history.Select(ToMessage).ToList(), cancellationToken: cancellationToken);
        return new AgentReply(model, ToEntries(response.Messages), ToUsage(response.Usage));
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

    private static ChatMessage ToMessage(AgentEntry entry)
    {
        if (entry.Role == AgentMessageRole.Tool)
        {
            return new ChatMessage(ChatRole.Tool, [new FunctionResultContent(entry.ToolCallId ?? "", entry.Text)]);
        }

        if (entry.Role != AgentMessageRole.Assistant)
        {
            return new ChatMessage(ChatRole.User, entry.Text);
        }

        var contents = new List<AIContent>();
        if (entry.Text.Length > 0)
        {
            contents.Add(new TextContent(entry.Text));
        }

        foreach (var call in entry.ToolCalls ?? [])
        {
            contents.Add(new FunctionCallContent(call.Id, call.Name, JsonSerializer.Deserialize<Dictionary<string, object?>>(call.Arguments)));
        }

        return new ChatMessage(ChatRole.Assistant, contents);
    }

    private static List<AgentEntry> ToEntries(IEnumerable<ChatMessage> messages)
    {
        var entries = new List<AgentEntry>();
        var toolNames = new Dictionary<string, string>();

        foreach (var message in messages)
        {
            if (message.Role == ChatRole.Tool)
            {
                foreach (var result in message.Contents.OfType<FunctionResultContent>())
                {
                    entries.Add(new AgentEntry(AgentMessageRole.Tool, ResultText(result.Result), ToolCallId: result.CallId,
                        ToolName: toolNames.GetValueOrDefault(result.CallId, "")));
                }

                continue;
            }

            if (message.Role != ChatRole.Assistant)
            {
                continue;
            }

            var calls = message.Contents.OfType<FunctionCallContent>()
                .Select(c => new AgentToolCall(c.CallId, c.Name, JsonSerializer.Serialize(c.Arguments ?? new Dictionary<string, object?>())))
                .ToList();
            foreach (var call in calls)
            {
                toolNames[call.Id] = call.Name;
            }

            entries.Add(new AgentEntry(AgentMessageRole.Assistant, message.Text.Trim(), calls.Count == 0 ? null : calls));
        }

        return entries;
    }

    private static string ResultText(object? result)
    {
        return result switch
        {
            null => "",
            string text => text,
            JsonElement { ValueKind: JsonValueKind.String } json => json.GetString() ?? "",
            _ => result.ToString() ?? "",
        };
    }

    private static LanguageModelUsage? ToUsage(UsageDetails? usage)
    {
        return usage is null
            ? null
            : new LanguageModelUsage(usage.InputTokenCount ?? 0, usage.OutputTokenCount ?? 0, usage.CachedInputTokenCount ?? 0, 0);
    }
}
