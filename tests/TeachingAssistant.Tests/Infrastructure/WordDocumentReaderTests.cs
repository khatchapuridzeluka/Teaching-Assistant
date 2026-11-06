using System.IO.Compression;
using System.Text;
using TeachingAssistant.Infrastructure.Documents;

namespace TeachingAssistant.Tests.Infrastructure;

public class WordDocumentReaderTests
{
    private const string Namespaces =
        "xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\" " +
        "xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\" " +
        "xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\" " +
        "xmlns:v=\"urn:schemas-microsoft-com:vml\" " +
        "xmlns:mc=\"http://schemas.openxmlformats.org/markup-compatibility/2006\"";

    private readonly WordDocumentReader _reader = new();

    [Fact]
    public void Read_KeepsParagraphsListItemsAndTableRows()
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

        var document = _reader.Read(docx);

        Assert.Equal(
            "Homework 2: At the Zoo\n- All fields must be private.\nTask 1: Animal | 15\nTotal | 100",
            document!.Text.ReplaceLineEndings("\n"));
        Assert.Empty(document.Images);
    }

    [Fact]
    public void Read_MarksEmbeddedPicturesWhereTheyAppear_AndReturnsThem()
    {
        // Instructions often paste code as screenshots; the marker tells the model which task each one belongs to.
        var docx = Docx(
            """
            <w:p><w:r><w:t>Task 1</w:t></w:r></w:p>
            <w:p><w:r><w:drawing><a:blip r:embed="rId4"/></w:drawing></w:r></w:p>
            <w:p><w:r><w:t>Task 2</w:t></w:r></w:p>
            <w:p><w:r><w:drawing><a:blip r:embed="rId5"/></w:drawing></w:r></w:p>
            <w:p><w:r><w:drawing><a:blip r:embed="rId4"/></w:drawing></w:r></w:p>
            """,
            relationships:
            """
            <Relationship Id="rId4" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/image" Target="media/image1.png"/>
            <Relationship Id="rId5" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/image" Target="media/image2.jpeg"/>
            """,
            media: new() { ["word/media/image1.png"] = [1, 2, 3], ["word/media/image2.jpeg"] = [4, 5] });

        var document = _reader.Read(docx)!;

        // The same picture used twice keeps its number and is returned once.
        Assert.Equal("Task 1\n[image 1]\nTask 2\n[image 2]\n[image 1]", document.Text.ReplaceLineEndings("\n"));
        Assert.Equal([("image1.png", "image/png"), ("image2.jpeg", "image/jpeg")],
            document.Images.Select(i => (i.Name, i.MediaType)));
        Assert.Equal([1, 2, 3], document.Images[0].Content);
    }

    [Fact]
    public void Read_IgnoresLinkedPicturesAndFallbackCopies_AndMarksUnreadableFormats()
    {
        var docx = Docx(
            """
            <w:p><w:r><mc:AlternateContent>
              <mc:Choice><w:drawing><a:blip r:embed="rId4"/></w:drawing></mc:Choice>
              <mc:Fallback><w:pict><v:imagedata r:id="rId4"/></w:pict></mc:Fallback>
            </mc:AlternateContent></w:r></w:p>
            <w:p><w:r><w:drawing><a:blip r:embed="rId6"/></w:drawing></w:r></w:p>
            <w:p><w:r><w:drawing><a:blip r:embed="rId7"/></w:drawing></w:r></w:p>
            """,
            relationships:
            """
            <Relationship Id="rId4" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/image" Target="media/image1.png"/>
            <Relationship Id="rId6" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/image" Target="https://example.com/x.png" TargetMode="External"/>
            <Relationship Id="rId7" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/image" Target="media/image3.emf"/>
            """,
            media: new() { ["word/media/image1.png"] = [1], ["word/media/image3.emf"] = [2] });

        var document = _reader.Read(docx)!;

        Assert.Equal("[image 1]\n\n[an image in .emf format that could not be included]", document.Text.ReplaceLineEndings("\n"));
        Assert.Single(document.Images);
    }

    [Fact]
    public void Read_ReturnsNullForAFileThatIsNotADocx()
    {
        Assert.Null(_reader.Read(Encoding.UTF8.GetBytes("not a zip")));
    }

    [Fact]
    public void Read_RefusesDocumentTypeDefinitions()
    {
        // A DTD could expand entities or reach outside the file; student files are untrusted.
        var docx = Docx("<w:p><w:r><w:t>&x;</w:t></w:r></w:p>", doctype: "<!DOCTYPE w:document [<!ENTITY x \"boom\">]>");

        Assert.Null(_reader.Read(docx));
    }

    [Theory]
    [InlineData("Homework.docx", null, true)]
    [InlineData("homework", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", true)]
    [InlineData("Homework.doc", "application/msword", false)]
    public void CanRead_JudgesByNameOrType(string name, string? mediaType, bool expected)
    {
        Assert.Equal(expected, _reader.CanRead(name, mediaType));
    }

    // A minimal .docx: a zip with word/document.xml holding the given body, and optionally relationships and media files.
    private static byte[] Docx(
        string body, string doctype = "", string? relationships = null, Dictionary<string, byte[]>? media = null)
    {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            Write(zip, "word/document.xml", $"""
                <?xml version="1.0" encoding="UTF-8"?>{doctype}
                <w:document {Namespaces}><w:body>{body}</w:body></w:document>
                """);

            if (relationships is not null)
            {
                Write(zip, "word/_rels/document.xml.rels", $"""
                    <?xml version="1.0" encoding="UTF-8"?>
                    <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">{relationships}</Relationships>
                    """);
            }

            foreach (var (path, bytes) in media ?? [])
            {
                using var stream = zip.CreateEntry(path).Open();
                stream.Write(bytes);
            }
        }

        return buffer.ToArray();
    }

    private static void Write(ZipArchive zip, string path, string text)
    {
        using var writer = new StreamWriter(zip.CreateEntry(path).Open());
        writer.Write(text);
    }
}
