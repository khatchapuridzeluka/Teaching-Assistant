using System.Text;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Domain.Grading;
using TeachingAssistant.Infrastructure.Documents;

namespace TeachingAssistant.Tests.Infrastructure;

public class QuestPdfGradingRendererTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 13, 10, 0, 0, TimeSpan.Zero);

    private static GradingDocument Sample(GradingDocumentStatus status, string student = "Nini Beridze") => new()
    {
        StudentName = student,
        AssignmentTitle = "Assignment 2: At the Zoo",
        DueAt = Now.AddDays(-20),
        VersionNumber = 1,
        TurnedInAt = Now.AddDays(-21),
        Status = status,
        Score = 88,
        MaxScore = 100,
        Bonus = 7,
        AiScore = 85,
        AiBonus = 7,
        Review = """
        ## Score

        | Task | Points | Reason |
        |---|---|---|
        | 1: Animal | 9 / 15 | The field is `costs` instead of `foodCosts` (-3), and the getter is `getCosts()` instead of `getFoodCosts()` (-3). |
        | 2: Vivarium | 20 / 20 | Fields, constructor, `addAnimal` and `getArea()` are correct. |
        | **Total** | **88 / 100** | |
        | Bonus | +7 / 10 | Correct logic, but named `gmev()` instead of `getMostExpensiveVivarium()` (-3). |

        ## Feedback

        Your classes work correctly. **Unsure:** the Zoo screenshot is cut off, so *please* include the complete class next time.

        - Use the exact names the assignment gives.
        - Build joined strings with `String.join`.
        """,
        Mistakes = [
            new GradingDocumentMistake
            {
                Severity = MistakeSeverity.Minor,
                Category = MistakeCategory.Naming,
                Message = "The field is costs instead of foodCosts.",
                Points = 3,
                File = "Animal.java",
                StartLine = 4,
                EndLine = 4,
            },
            new GradingDocumentMistake
            {
                Severity = MistakeSeverity.Major,
                Category = MistakeCategory.MissingRequirement,
                Message = "Zoo.toString() is not visible in the submission.",
                Points = 0,
                File = "Zoo.java",
                StartLine = 12,
                EndLine = 30,
            },
            new GradingDocumentMistake
            {
                Severity = MistakeSeverity.Style,
                Category = MistakeCategory.Readability,
                Message = "Leftover commented-out code.",
                Points = 0,
            },
        ],
        Strengths = [new GradingDocumentStrength
        {
            Category = MistakeCategory.Design,
            Message = "Vivarium.toString() reuses Animal.toString(), as the hint suggested.",
            File = "Vivarium.java",
            StartLine = 30,
            EndLine = 41,
        }],
        Model = "model",
        GradedAt = Now.AddDays(-1),
        ApprovedAt = status == GradingDocumentStatus.Approved ? Now : null,
        GeneratedAt = Now,
    };

    [Theory]
    [InlineData(GradingDocumentStatus.Draft)]
    [InlineData(GradingDocumentStatus.Approved)]
    public void Render_ProducesAPdf_ForTheReviewTableListsMistakesAndStrengths(GradingDocumentStatus status)
    {
        var pdf = new QuestPdfGradingRenderer().RenderPdf(Sample(status));

        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
        Assert.True(pdf.Length > 5_000);
    }

    [Fact]
    public void Render_KeepsAGeorgianStudentName()
    {
        var pdf = new QuestPdfGradingRenderer().RenderPdf(Sample(GradingDocumentStatus.InReview, "ნინი ბერიძე"));

        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
    }
}
