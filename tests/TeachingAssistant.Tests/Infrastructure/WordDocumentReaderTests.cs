using System.IO.Compression;
using System.Text;
using TeachingAssistant.Infrastructure.Documents;

namespace TeachingAssistant.Tests.Infrastructure;

public class WordDocumentReaderTests
{
    private readonly WordDocumentReader _reader = new();

    [Fact]
    public void ReadText_KeepsParagraphsListItemsAndTableRows()
    {
        var docx = Docx(
            """
            <w:p><w:r><w:t>Homework 2: </w:t></w:r><w:r><w:t>At the Zoo</w:t></w:r></w:p>
            <w:p><w:pPr><w:numPr><w:ilvl w:val="0"/></w:numPr></w:pPr><w:r><w:t>All fields must be private.</w:t></w:r></w:p>
            <w:tbl>
              <w:tr><w:tc><w:p><w:r><w:t>Task 1: Animal</w:t></w:r></w:p></w:tc><w:tc><w:p><w:r><w:t>15</w:t></w:r></w:p></w:tc></w:tr>
              <w:tr><w:tc><w:p><w:r><w:t>Total</w:t></w:r></w:p></w:tc><w:tc><w:p><w:r><w:t>100</w:t></w:r></w:p></w:tc></w:tr>
            </w:tbl>
            """);

        var text = _reader.ReadText(docx);

        Assert.Equal(
            "Homework 2: At the Zoo\n- All fields must be private.\nTask 1: Animal | 15\nTotal | 100",
            text!.ReplaceLineEndings("\n"));
    }

    [Fact]
    public void ReadText_ReturnsNullForAFileThatIsNotADocx()
    {
        Assert.Null(_reader.ReadText(Encoding.UTF8.GetBytes("not a zip")));
    }

    [Fact]
    public void ReadText_RefusesDocumentTypeDefinitions()
    {
        // A DTD could expand entities or reach outside the file; student files are untrusted.
        var docx = Docx("<w:p><w:r><w:t>&x;</w:t></w:r></w:p>", "<!DOCTYPE w:document [<!ENTITY x \"boom\">]>");

        Assert.Null(_reader.ReadText(docx));
    }

    [Theory]
    [InlineData("Homework.docx", null, true)]
    [InlineData("homework", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", true)]
    [InlineData("Homework.doc", "application/msword", false)]
    public void CanRead_JudgesByNameOrType(string name, string? mediaType, bool expected)
    {
        Assert.Equal(expected, _reader.CanRead(name, mediaType));
    }

    // A minimal .docx: a zip with word/document.xml holding the given body.
    private static byte[] Docx(string body, string doctype = "")
    {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            using var writer = new StreamWriter(zip.CreateEntry("word/document.xml").Open());
            writer.Write($"""
                <?xml version="1.0" encoding="UTF-8"?>{doctype}
                <w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:body>{body}</w:body></w:document>
                """);
        }

        return buffer.ToArray();
    }
}
