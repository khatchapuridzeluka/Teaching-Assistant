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
    private const string MissingResult = "No result was stored for this tool call; treat it as not run.";

    public static IChatClient ChatClient(AnthropicClient client, string model, int maxTokens)
    {
        return client.AsIChatClient(model, maxTokens);
    }

    public static async Task<AgentReply> RunAsync(
        IChatClient chatClient, string model, string systemPrompt, IReadOnlyList<AgentEntry> history, IReadOnlyList<object> tools,
        AgentRunHooks? hooks, CancellationToken cancellationToken)
    {
        var messages = ToMessages(history, out var answered);
        var agent = new ChatClientAgent(
            hooks is null ? chatClient : new StepObserver(chatClient, hooks, answered),
            instructions: systemPrompt,
            name: "teaching-assistant",
            tools: tools.SelectMany(t => ToFunctions(t, hooks, answered)).ToList());

        var response = await agent.RunAsync(messages, cancellationToken: cancellationToken);
        return new AgentReply { Model = model, Entries = ToEntries(response.Messages), Usage = ToUsage(response.Usage) };
    }

    private static IEnumerable<AITool> ToFunctions(object tools, AgentRunHooks? hooks, HashSet<string> answered)
    {
        foreach (var method in tools.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance))
        {
            if (method.GetCustomAttribute<AgentToolAttribute>() is { } tool)
            {
                var function = AIFunctionFactory.Create(method, tools, tool.Name);
                yield return hooks is null ? function : new ObservedFunction(function, hooks, answered);
            }
        }
    }

    /// <summary>
    /// Turns the stored conversation into the model's messages. The model requires every tool call to be followed by its
    /// result; a call whose result was never stored (an older run lost it) gets a placeholder result, so one lost step never
    /// breaks the rest of the conversation. <paramref name="answered"/> collects every call id that already has a result.
    /// </summary>
    private static List<ChatMessage> ToMessages(IReadOnlyList<AgentEntry> history, out HashSet<string> answered)
    {
        answered = history.Where(e => e is { Role: AgentMessageRole.Tool, ToolCallId: not null }).Select(e => e.ToolCallId!).ToHashSet();
        var messages = new List<ChatMessage>();
        var open = new List<string>();
        foreach (var entry in history)
        {
            if (entry.Role != AgentMessageRole.Tool)
            {
                CloseOpenCalls(messages, open, answered);
            }

            messages.Add(ToMessage(entry));
            if (entry.ToolCalls is { Count: > 0 } calls)
            {
                open.AddRange(calls.Select(c => c.Id));
            }
            else if (entry.ToolCallId is { } callId)
            {
                open.Remove(callId);
            }
        }

        CloseOpenCalls(messages, open, answered);
        return messages;
    }

    private static void CloseOpenCalls(List<ChatMessage> messages, List<string> open, HashSet<string> answered)
    {
        foreach (var callId in open)
        {
            messages.Add(new ChatMessage(ChatRole.Tool, [new FunctionResultContent(callId, MissingResult)]));
            answered.Add(callId);
        }

        open.Clear();
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
                    entries.Add(new AgentEntry
                    {
                        Role = AgentMessageRole.Tool,
                        Text = ResultText(result.Result),
                        ToolCallId = result.CallId,
                        ToolName = toolNames.GetValueOrDefault(result.CallId, ""),
                    });
                }

                continue;
            }

            if (ToAssistantEntry(message) is { } entry)
            {
                foreach (var call in entry.ToolCalls ?? [])
                {
                    toolNames[call.Id] = call.Name;
                }

                entries.Add(entry);
            }
        }

        return entries;
    }

    private static AgentEntry? ToAssistantEntry(ChatMessage message)
    {
        if (message.Role != ChatRole.Assistant)
        {
            return null;
        }

        var calls = message.Contents.OfType<FunctionCallContent>()
            .Select(c => new AgentToolCall
            {
                Id = c.CallId,
                Name = c.Name,
                Arguments = JsonSerializer.Serialize(c.Arguments ?? new Dictionary<string, object?>()),
            })
            .ToList();
        return new AgentEntry
        {
            Role = AgentMessageRole.Assistant,
            Text = message.Text.Trim(),
            ToolCalls = calls.Count == 0 ? null : calls,
        };
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
            : new LanguageModelUsage
            {
                InputTokens = usage.InputTokenCount ?? 0,
                OutputTokens = usage.OutputTokenCount ?? 0,
                CacheReadTokens = usage.CachedInputTokenCount ?? 0,
                CacheWriteTokens = 0,
            };
    }

    private sealed class StepObserver(IChatClient inner, AgentRunHooks hooks, HashSet<string> answered) : DelegatingChatClient(inner)
    {
        public override async Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            var sent = messages.ToList();
            await ReportUnstoredResultsAsync(sent);
            var response = await base.GetResponseAsync(sent, options, cancellationToken);
            foreach (var message in response.Messages)
            {
                if (ToAssistantEntry(message) is { } entry)
                {
                    await hooks.OnStep(entry);
                }
            }

            return response;
        }

        /// <summary>
        /// Stores the tool results the model is about to read that no tool reported, before the model's next step: those the
        /// framework wrote itself when a call could not run (arguments it could not read or bind). Without them the stored
        /// conversation would hold a call without a result, which the model refuses on the next turn.
        /// </summary>
        private async Task ReportUnstoredResultsAsync(List<ChatMessage> messages)
        {
            var names = messages.SelectMany(m => m.Contents.OfType<FunctionCallContent>())
                .GroupBy(c => c.CallId)
                .ToDictionary(g => g.Key, g => g.Last().Name);
            foreach (var result in messages.Where(m => m.Role == ChatRole.Tool).SelectMany(m => m.Contents.OfType<FunctionResultContent>()))
            {
                if (!answered.Add(result.CallId))
                {
                    continue;
                }

                var text = ResultText(result.Result);
                if (text.Length == 0 && result.Exception is { } exception)
                {
                    text = $"Failed: {exception.Message}";
                }

                await hooks.OnStep(new AgentEntry
                {
                    Role = AgentMessageRole.Tool,
                    Text = text,
                    ToolCallId = result.CallId,
                    ToolName = names.GetValueOrDefault(result.CallId, "unknown"),
                });
            }
        }
    }

    private sealed class ObservedFunction(AIFunction inner, AgentRunHooks hooks, HashSet<string> answered) : DelegatingAIFunction(inner)
    {
        private const string Stopped = "Not run: this turn is ending, so no more tools are called.";

        protected override async ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
        {
            var context = FunctionInvokingChatClient.CurrentContext;
            object? result;
            if (hooks.ShouldStop())
            {
                result = Stopped;
            }
            else
            {
                try
                {
                    result = await base.InvokeCoreAsync(arguments, cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // A call the tool could not run, for example with a missing or malformed argument: the model reads why and
                    // can call again, and the result is stored like any other.
                    result = $"Failed: the tool could not run: {ex.Message}";
                }
            }

            var callId = context?.CallContent.CallId;
            if (callId is not null)
            {
                answered.Add(callId);
            }

            await hooks.OnStep(new AgentEntry
            {
                Role = AgentMessageRole.Tool,
                Text = ResultText(result),
                ToolCallId = callId,
                ToolName = Name,
            });
            if (hooks.ShouldStop() && context is not null)
            {
                context.Terminate = true;
            }

            return result;
        }
    }
}
