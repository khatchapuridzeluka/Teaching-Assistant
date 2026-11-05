using TeachingAssistant.Domain.Ingestion;

namespace TeachingAssistant.Tests.Domain;

public class SubmissionVersionTests
{
    [Fact]
    public void RecordExtraction_MarksExtractedOriginals_AndCanBeRepeated()
    {
        var submission = new Submission(SubmissionSource.GoogleClassroom, "c", "a", "s", "Ana K.", "sub", "folder", DateTimeOffset.UtcNow);
        var archive = Content("hw.zip", ContentKind.CodeArchive);
        var report = Content("report.pdf", ContentKind.Document);
        var version = submission.AddVersion(VersionStatus.Ready, null, null, "folder/v1", null, DateTimeOffset.UtcNow, [archive, report]);

        version.RecordExtraction("hash-1", ["hw.zip"], null);
        Assert.Equal("hash-1", version.ContentHash);
        Assert.Equal(ContentStatus.Extracted, archive.Status);
        Assert.Equal(ContentStatus.Stored, report.Status);

        // Re-extraction failed this time: the archive goes back to merely stored.
        version.RecordExtraction(null, [], "Could not extract 'hw.zip'.");
        Assert.Null(version.ContentHash);
        Assert.Equal(ContentStatus.Stored, archive.Status);
        Assert.Equal("Could not extract 'hw.zip'.", version.ExtractionDetail);
    }

    private static VersionContent Content(string name, ContentKind kind) =>
        new(ContentOrigin.Drive, kind, name, name, null, "md5", null, 1, name, null, ContentStatus.Stored);
}
