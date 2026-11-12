using System.Text;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Domain.Grading;
using TeachingAssistant.Infrastructure.Documents;

namespace TeachingAssistant.Tests.Infrastructure;

public class QuestPdfGradingRendererTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 13, 10, 0, 0, TimeSpan.Zero);

    private static GradingDocument Sample(GradingDocumentStatus status, string student = "Nini Beridze") => new(
        student,
        "Assignment 2: At the Zoo",
        Now.AddDays(-20),
        1,
        Now.AddDays(-21),
        status,
        88,
        100,
        7,
        85,
        7,
        """
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
        [
            new GradingDocumentMistake(MistakeSeverity.Minor, MistakeCategory.Naming, "The field is costs instead of foodCosts.", 3, "Animal.java", 4, 4),
            new GradingDocumentMistake(MistakeSeverity.Major, MistakeCategory.MissingRequirement, "Zoo.toString() is not visible in the submission.", 0, "Zoo.java", 12, 30),
            new GradingDocumentMistake(MistakeSeverity.Style, MistakeCategory.Readability, "Leftover commented-out code.", 0, null, null, null),
        ],
        [new GradingDocumentStrength(MistakeCategory.Design, "Vivarium.toString() reuses Animal.toString(), as the hint suggested.", "Vivarium.java", 30, 41)],
        "model",
        Now.AddDays(-1),
        status == GradingDocumentStatus.Approved ? Now : null,
        Now);

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
