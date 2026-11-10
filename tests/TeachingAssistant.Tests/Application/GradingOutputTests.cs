using TeachingAssistant.Application.Services;
using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Tests.Application;

public class GradingOutputTests
{
    private static readonly Dictionary<string, int> CodeLines = new() { ["src/Shape.java"] = 40 };

    [Fact]
    public void Strengths_AreRead_LocatedLikeMistakes_EmptyOnesDropped_AndCappedAtThree()
    {
        const string reply =
            """
            {
              "review": "**Score**", "score": 90, "maxScore": 100, "bonus": null, "mistakes": [],
              "strengths": [
                { "file": "Shape.java", "startLine": 3, "endLine": 99, "category": "Design", "message": " Abstract base class. " },
                { "file": null, "startLine": null, "endLine": null, "category": "ErrorHandling", "message": "Validates input." },
                { "file": "Shape.java", "startLine": 1, "endLine": 1, "category": "Naming", "message": " " },
                { "file": "Missing.java", "startLine": 5, "endLine": 5, "category": "Readability", "message": "Clear loops." },
                { "file": null, "startLine": null, "endLine": null, "category": "Other", "message": "A fourth one." }
              ]
            }
            """;

        var read = GradingOutput.Read(reply, CodeLines);

        Assert.Equal(3, read.Strengths.Count);
        Assert.Equal(("src/Shape.java", (int?)3, (int?)40, MistakeCategory.Design, "Abstract base class."),
            (read.Strengths[0].File, read.Strengths[0].StartLine, read.Strengths[0].EndLine, read.Strengths[0].Category,
                read.Strengths[0].Message));
        Assert.Null(read.Strengths[1].File);
        Assert.Equal(((string?)null, (int?)null, "Clear loops."), (read.Strengths[2].File, read.Strengths[2].StartLine, read.Strengths[2].Message));
    }

    [Fact]
    public void AReplyWithoutStrengths_HasNone_AndTheSchemaRequiresThem()
    {
        var read = GradingOutput.Read("""{ "review": "r", "score": 1, "maxScore": 2, "bonus": null, "mistakes": [] }""", CodeLines);

        Assert.Empty(read.Strengths);
        Assert.Contains("\"strengths\"]", GradingOutput.Schema);
    }
}
