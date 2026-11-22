using System.Text.Json;
using System.Text.Json.Serialization;
using TeachingAssistant.Application.Contracts;
using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Application.Services;

internal static class GradingOutput
{
    private static readonly string Severities = JsonSerializer.Serialize(Enum.GetNames<MistakeSeverity>());
    private static readonly string Categories = JsonSerializer.Serialize(Enum.GetNames<MistakeCategory>());

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
                  },
                  "points": {
                    "type": "number",
                    "description": "Points this mistake took off, as a positive number; 0 for Style."
                  }
                },
                "required": ["file", "startLine", "endLine", "severity", "category", "message", "points"]
              }
            },
            "strengths": {
              "type": "array",
              "description": "Up to three specific skills the code shows were done well; empty only when nothing stands out.",
              "items": {
                "type": "object",
                "additionalProperties": false,
                "properties": {
                  "file": {
                    "anyOf": [{ "type": "string" }, { "type": "null" }],
                    "description": "Path of the file exactly as in <file path=\"...\">, or null for a strength of the whole submission."
                  },
                  "startLine": {
                    "anyOf": [{ "type": "integer" }, { "type": "null" }],
                    "description": "First line that shows it, using the line numbers shown in the code; null when file is null."
                  },
                  "endLine": {
                    "anyOf": [{ "type": "integer" }, { "type": "null" }],
                    "description": "Last line that shows it (same as startLine for one line); null when file is null."
                  },
                  "category": { "type": "string", "enum": {{Categories}} },
                  "message": {
                    "type": "string",
                    "description": "One sentence: what was done and why it is good."
                  }
                },
                "required": ["file", "startLine", "endLine", "category", "message"]
              }
            }
          },
          "required": ["review", "score", "maxScore", "bonus", "mistakes", "strengths"]
        }
        """;

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

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
            return new ReadReply { Review = reply, Mistakes = [], Strengths = [] };
        }

        var mistakes = (parsed.Mistakes ?? [])
            .Where(m => !string.IsNullOrWhiteSpace(m.Message))
            .Select(m => Locate(m, codeLines))
            .ToList();
        var strengths = (parsed.Strengths ?? [])
            .Where(s => !string.IsNullOrWhiteSpace(s.Message))
            .Take(MaxStrengths)
            .Select(s => Locate(s, codeLines))
            .ToList();
        return new ReadReply
        {
            Review = parsed.Review,
            Score = parsed.Score,
            MaxScore = parsed.MaxScore,
            Bonus = parsed.Bonus,
            Mistakes = mistakes,
            Strengths = strengths,
        };
    }

    private const int MaxStrengths = 3;

    private static CodeStrength Locate(ReplyStrength strength, IReadOnlyDictionary<string, int> codeLines)
    {
        var (file, start, end) = Lines(strength.File, strength.StartLine, strength.EndLine, codeLines);
        return new CodeStrength
        {
            File = file,
            StartLine = start,
            EndLine = end,
            Category = strength.Category,
            Message = strength.Message!.Trim(),
        };
    }

    private static (string? File, int? Start, int? End) Lines(
        string? path, int? startLine, int? endLine, IReadOnlyDictionary<string, int> codeLines)
    {
        var file = ResolveFile(path, codeLines.Keys);
        if (file is not null && startLine is { } first && first >= 1 && first <= codeLines[file])
        {
            return (file, first, Math.Clamp(endLine ?? first, first, codeLines[file]));
        }

        return (file, null, null);
    }

    private static CodeMistake Locate(ReplyMistake mistake, IReadOnlyDictionary<string, int> codeLines)
    {
        var (file, start, end) = Lines(mistake.File, mistake.StartLine, mistake.EndLine, codeLines);

        double? points = mistake.Points is { } value ? Math.Abs(value) : null;
        return new CodeMistake
        {
            File = file,
            StartLine = start,
            EndLine = end,
            Severity = mistake.Severity,
            Category = mistake.Category,
            Message = mistake.Message!.Trim(),
            Points = points,
        };
    }

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

    public sealed record ReadReply
    {
        public required string Review { get; init; }
        public double? Score { get; init; }
        public double? MaxScore { get; init; }
        public double? Bonus { get; init; }
        public required IReadOnlyList<CodeMistake> Mistakes { get; init; }
        public required IReadOnlyList<CodeStrength> Strengths { get; init; }
    }

    private sealed record Reply
    {
        public string? Review { get; init; }
        public double? Score { get; init; }
        public double? MaxScore { get; init; }
        public double? Bonus { get; init; }
        public List<ReplyMistake>? Mistakes { get; init; }
        public List<ReplyStrength>? Strengths { get; init; }
    }

    private sealed record ReplyStrength
    {
        public string? File { get; init; }
        public int? StartLine { get; init; }
        public int? EndLine { get; init; }
        public required MistakeCategory Category { get; init; }
        public string? Message { get; init; }
    }

    private sealed record ReplyMistake
    {
        public string? File { get; init; }
        public int? StartLine { get; init; }
        public int? EndLine { get; init; }
        public required MistakeSeverity Severity { get; init; }
        public required MistakeCategory Category { get; init; }
        public string? Message { get; init; }
        public double? Points { get; init; }
    }
}
