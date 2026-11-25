using TeachingAssistant.Application.Prompts;

namespace TeachingAssistant.Tests.Application;

public class PromptLibraryTests
{
    [Fact]
    public void EveryPromptFile_IsEmbedded_AndReadsWithPlainLineEndings()
    {
        Assert.Equal(["agent", "class-mistakes", "grading", "student-mistakes", "student-overview", "student-strengths", "test-adapter", "test-code", "tests"],
            PromptLibrary.Names());

        foreach (var name in PromptLibrary.Names())
        {
            var text = PromptLibrary.Get(name);
            Assert.False(string.IsNullOrWhiteSpace(text), name);
            Assert.DoesNotContain('\r', text);
            Assert.Equal(text.TrimEnd(), text);
        }
    }

    [Fact]
    public void ThePromptsWithoutSlots_HaveNoPlaceholderLeft()
    {
        foreach (var text in new[]
                 {
                     PromptLibrary.Agent, PromptLibrary.ClassMistakes, PromptLibrary.StudentMistakes,
                     PromptLibrary.StudentStrengths, PromptLibrary.StudentOverview,
                 })
        {
            Assert.DoesNotContain("{{", text);
        }
    }

    [Fact]
    public void Grading_PutsTheRubricBetweenTheIntroAndTheOutputRules()
    {
        var prompt = PromptLibrary.Grading("How to grade:\n- Be fair.");

        Assert.StartsWith("You grade programming homework", prompt);
        Assert.Contains("say when you are unsure instead of sounding certain.\n\nWhen the message has a <build_and_test_results> section", prompt);
        Assert.Contains("Without that section, judge from the code.\n\nWhen the message has an <automated_tests> section", prompt);
        Assert.Contains("so judge from the code as usual.\n\nHow to grade:\n- Be fair.\n\nYour answer is in English", prompt);
        Assert.EndsWith("the teacher reviews a whole class.", prompt);
    }

    [Fact]
    public void Render_RefusesAnUnfilledOrUnknownPlaceholder()
    {
        Assert.Throws<InvalidOperationException>(() => PromptLibrary.Render("grading"));
        Assert.Throws<InvalidOperationException>(() => PromptLibrary.Render("agent", ("rubric", "x")));
        Assert.Throws<InvalidOperationException>(() => PromptLibrary.Get("missing"));
    }
}
