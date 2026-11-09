namespace TeachingAssistant.Application.Models;

public record LanguageModelReply(string Model, string Text, LanguageModelUsage? Usage = null);

public record LanguageModelUsage(long InputTokens, long OutputTokens, long CacheReadTokens, long CacheWriteTokens);
