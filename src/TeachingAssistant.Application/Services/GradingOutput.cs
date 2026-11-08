using System.Text.Json;
using System.Text.Json.Serialization;
using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Application.Services;

/// <summary>
/// The shape of the grading model's reply (a JSON Schema for structured output) and how the reply is read back: parsed
/// into records, and every mistake's file and lines checked against the code that was actually sent, so the code viewer
/// never points at a line that does not exist.
/// </summary>
internal static class GradingOutput
{
    private static readonly string Severities = JsonSerializer.Serialize(Enum.GetNames<MistakeSeverity>());
    private static readonly string Categories = JsonSerializer.Serialize(Enum.GetNames<MistakeCategory>());

    // Structured output needs additionalProperties: false on every object; nullable fields use anyOf with null.
    public static readonly string Schema =
        $$"""
        {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "review": {
              "type": "string",
              "description": "The review in Markdown with the Score and Feedback sections."
            },
            "score": {
              "anyOf": [{ "type": "number" }, { "type": "null" }],
              "description": "Total points awarded, the same total as in the Score section, without the bonus."
            },
            "maxScore": {
              "anyOf": [{ "type": "number" }, { "type": "null" }],
              "description": "The maximum points the score is out of (the assignment's maximum)."
            },
            "bonus": {
              "anyOf": [{ "type": "number" }, { "type": "null" }],
              "description": "Bonus points awarded, kept apart from the score; null when the assignment has no bonus or none was earned."
            },
            "mistakes": {
              "type": "array",
              "description": "Every mistake, one entry each.",
              "items": {
                "type": "object",
                "additionalProperties": false,
                "properties": {
                  "file": {
                    "anyOf": [{ "type": "string" }, { "type": "null" }],
                    "description": "Path of the file exactly as in <file path=\"...\">, or null for a mistake about the whole submission."
                  },
                  "startLine": {
                    "anyOf": [{ "type": "integer" }, { "type": "null" }],
                    "description": "First line of the mistake, using the line numbers shown in the code; null when file is null."
                  },
                  "endLine": {
                    "anyOf": [{ "type": "integer" }, { "type": "null" }],
                    "description": "Last line of the mistake (same as startLine for one line); null when file is null."
                  },
                  "severity": { "type": "string", "enum": {{Severities}} },
                  "category": { "type": "string", "enum": {{Categories}} },
                  "message": {
                    "type": "string",
                    "description": "What is wrong and how to fix it, one or two sentences, and which task it costs points in, or that it cost no points (always for Style)."
                  }
                },
                "required": ["file", "startLine", "endLine", "severity", "category", "message"]
              }
            }
          },
          "required": ["review", "score", "maxScore", "bonus", "mistakes"]
        }
        """;

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>
    /// Reads the model's reply. codeLines maps every code file that was sent to its number of lines. A reply that is not
    /// the expected JSON (structured output makes that rare) is shown as the review itself, with no score or mistakes.
    /// </summary>
    public static ReadReply Read(string reply, IReadOnlyDictionary<string, int> codeLines)
    {
        Reply? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<Reply>(reply, Options);
        }
        catch (JsonException)
        {
            parsed = null;
        }

        if (parsed?.Review is null)
        {
            return new ReadReply(reply, null, null, null, []);
        }

        var mistakes = (parsed.Mistakes ?? [])
            .Where(m => !string.IsNullOrWhiteSpace(m.Message))
            .Select(m => Locate(m, codeLines))
            .ToList();
        return new ReadReply(parsed.Review, parsed.Score, parsed.MaxScore, parsed.Bonus, mistakes);
    }

    // Keeps the model's file and lines only where they exist in the code that was sent.
    private static CodeMistake Locate(ReplyMistake mistake, IReadOnlyDictionary<string, int> codeLines)
    {
        var file = ResolveFile(mistake.File, codeLines.Keys);
        int? start = null, end = null;
        if (file is not null && mistake.StartLine is { } first && first >= 1 && first <= codeLines[file])
        {
            start = first;
            end = Math.Clamp(mistake.EndLine ?? first, first, codeLines[file]);
        }

        return new CodeMistake(file, start, end, mistake.Severity, mistake.Category, mistake.Message!.Trim());
    }

    // The exact path, else the same path in another case, else the one file whose path ends with it (the model
    // sometimes writes "Zoo.java" for "src/Zoo.java"). Null when nothing or more than one file matches.
    private static string? ResolveFile(string? path, IEnumerable<string> files)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var wanted = path.Trim().Replace('\\', '/').TrimStart('.', '/');
        var all = files.ToList();
        var exact = all.FirstOrDefault(f => f == wanted) ?? all.FirstOrDefault(f => string.Equals(f, wanted, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
        {
            return exact;
        }

        var bySuffix = all.Where(f => f.EndsWith("/" + wanted, StringComparison.OrdinalIgnoreCase)).ToList();
        return bySuffix.Count == 1 ? bySuffix[0] : null;
    }

    /// <summary>The model's answer as records: review text, score numbers and checked mistakes.</summary>
    public sealed record ReadReply(string Review, double? Score, double? MaxScore, double? Bonus, IReadOnlyList<CodeMistake> Mistakes);

    private sealed record Reply(string? Review, double? Score, double? MaxScore, double? Bonus, List<ReplyMistake>? Mistakes);

    private sealed record ReplyMistake(
        string? File, int? StartLine, int? EndLine, MistakeSeverity Severity, MistakeCategory Category, string? Message);
}
