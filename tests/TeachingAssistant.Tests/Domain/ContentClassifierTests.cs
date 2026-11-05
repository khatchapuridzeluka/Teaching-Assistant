using TeachingAssistant.Domain.Ingestion;

namespace TeachingAssistant.Tests.Domain;

public class ContentClassifierTests
{
    [Theory]
    [InlineData("homework-1.rar", "application/x-rar", ContentKind.CodeArchive)]
    [InlineData("untitled1.zip", "application/x-zip-compressed", ContentKind.CodeArchive)]
    [InlineData("HW.ZIP", null, ContentKind.CodeArchive)]
    [InlineData("Main.java", "text/x-java", ContentKind.SourceFile)]
    [InlineData("დავალება1prog.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", ContentKind.Document)]
    [InlineData("Report.pdf", "application/pdf", ContentKind.Document)]
    [InlineData("output.PNG", "image/png", ContentKind.Image)]
    [InlineData("video.mp4", "video/mp4", ContentKind.Other)]
    public void Classify_UsesTheExtensionFirst(string fileName, string? mimeType, ContentKind expected) =>
        Assert.Equal(expected, ContentClassifier.Classify(fileName, mimeType));

    [Theory]
    [InlineData("archive", "application/zip", ContentKind.CodeArchive)]
    [InlineData("screenshot", "image/jpeg", ContentKind.Image)]
    [InlineData("notes", "text/plain", ContentKind.Document)]
    [InlineData("blob", "application/octet-stream", ContentKind.Other)]
    [InlineData("blob", null, ContentKind.Other)]
    public void Classify_WithoutExtension_FallsBackToTheType(string fileName, string? mimeType, ContentKind expected) =>
        Assert.Equal(expected, ContentClassifier.Classify(fileName, mimeType));
}
