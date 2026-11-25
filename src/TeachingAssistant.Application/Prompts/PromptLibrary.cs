using System.Collections.Concurrent;

namespace TeachingAssistant.Application.Prompts;

/// <summary>
/// The model's fixed instructions. Each one is a Markdown file in this folder, embedded in the assembly, read once and
/// kept in memory. The files hold only the wording; what changes per call (the teacher's rubric layers, the student's
/// records, the output schemas) is still built in code. A <c>{{name}}</c> placeholder marks where code inserts a part.
/// </summary>
public static class PromptLibrary
{
    private const string ResourcePrefix = "TeachingAssistant.Application.Prompts.";

    private static readonly ConcurrentDictionary<string, string> Cache = new();

    /// <summary>The chat agent's system prompt (Prompts/agent.md).</summary>
    public static string Agent => Get("agent");

    /// <summary>The class-wide mistake analysis (Prompts/class-mistakes.md).</summary>
    public static string ClassMistakes => Get("class-mistakes");

    /// <summary>One student's mistakes across the course (Prompts/student-mistakes.md).</summary>
    public static string StudentMistakes => Get("student-mistakes");

    /// <summary>One student's strengths across the course (Prompts/student-strengths.md).</summary>
    public static string StudentStrengths => Get("student-strengths");

    /// <summary>One student's overall picture (Prompts/student-overview.md).</summary>
    public static string StudentOverview => Get("student-overview");

    /// <summary>Designing an assignment's test cases (Prompts/tests.md).</summary>
    public static string Tests => Get("tests");

    /// <summary>Writing an approved test set's JUnit code (Prompts/test-code.md).</summary>
    public static string TestCode => Get("test-code");

    /// <summary>Writing one student's test adapter (Prompts/test-adapter.md).</summary>
    public static string TestAdapter => Get("test-adapter");

    /// <summary>
    /// The grading system prompt (Prompts/grading.md) with the teacher's rubric layers in its <c>{{rubric}}</c> slot.
    /// </summary>
    public static string Grading(string rubric)
    {
        return Render("grading", ("rubric", rubric));
    }

    /// <summary>The names of every prompt file embedded in the assembly, without the .md extension.</summary>
    public static IReadOnlyList<string> Names()
    {
        return typeof(PromptLibrary).Assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(ResourcePrefix, StringComparison.Ordinal) && n.EndsWith(".md", StringComparison.Ordinal))
            .Select(n => n[ResourcePrefix.Length..^".md".Length])
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// The prompt's text with line endings normalized to \n and no trailing blank lines, so the model gets the same
    /// bytes whichever way git checked the file out.
    /// </summary>
    public static string Get(string name)
    {
        return Cache.GetOrAdd(name, Load);
    }

    /// <summary>The prompt with each <c>{{key}}</c> replaced; a placeholder left unfilled is a bug and throws.</summary>
    public static string Render(string name, params (string Key, string Value)[] values)
    {
        var text = Get(name);
        foreach (var (key, value) in values)
        {
            var placeholder = "{{" + key + "}}";
            if (!text.Contains(placeholder, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"The prompt '{name}' has no {placeholder} placeholder.");
            }

            text = text.Replace(placeholder, value, StringComparison.Ordinal);
        }

        var open = text.IndexOf("{{", StringComparison.Ordinal);
        if (open >= 0)
        {
            var close = text.IndexOf("}}", open, StringComparison.Ordinal);
            var placeholder = close < 0 ? text[open..] : text[open..(close + 2)];
            throw new InvalidOperationException($"The prompt '{name}' still has the placeholder {placeholder}.");
        }

        return text;
    }

    private static string Load(string name)
    {
        using var stream = typeof(PromptLibrary).Assembly.GetManifestResourceStream(ResourcePrefix + name + ".md")
                           ?? throw new InvalidOperationException($"The prompt file Prompts/{name}.md is not embedded.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().ReplaceLineEndings("\n").TrimEnd();
    }
}
