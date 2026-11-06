using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using TeachingAssistant.Application.Interfaces;

namespace TeachingAssistant.Infrastructure.Documents;

/// <summary>
/// <see cref="IDocumentReader"/> for Word .docx files. A .docx is a zip whose main part, word/document.xml, holds the
/// text; this reads it with the framework's zip and XML classes, no Office library. Formatting is dropped, but
/// paragraphs, list items and tables (such as a points table) keep their structure as plain text.
/// Files can come from students, so the XML is read without DTDs and with a size limit.
/// </summary>
public class WordDocumentReader : IDocumentReader
{
    private const string DocxMediaType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    // Characters read from document.xml at most: far above any homework text, small enough to stop a zip bomb.
    private const long MaxXmlCharacters = 20_000_000;

    private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    public bool CanRead(string fileName, string? mediaType) =>
        string.Equals(mediaType, DocxMediaType, StringComparison.OrdinalIgnoreCase)
        || fileName.EndsWith(".docx", StringComparison.OrdinalIgnoreCase);

    public string? ReadText(byte[] content)
    {
        try
        {
            using var zip = new ZipArchive(new MemoryStream(content), ZipArchiveMode.Read);
            var entry = zip.GetEntry("word/document.xml");
            if (entry is null)
            {
                return null;
            }

            using var stream = entry.Open();
            using var reader = XmlReader.Create(stream, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                MaxCharactersInDocument = MaxXmlCharacters,
            });
            var body = XDocument.Load(reader).Root?.Element(W + "body");
            if (body is null)
            {
                return null;
            }

            var text = new StringBuilder();
            foreach (var block in body.Elements())
            {
                if (block.Name == W + "p")
                {
                    text.AppendLine(Paragraph(block));
                }
                else if (block.Name == W + "tbl")
                {
                    AppendTable(text, block);
                }
            }

            return text.ToString().Trim();
        }
        catch (Exception ex) when (ex is InvalidDataException or XmlException)
        {
            return null; // not a zip, or broken XML: not a readable .docx
        }
    }

    private static string Paragraph(XElement paragraph)
    {
        var text = new StringBuilder();
        foreach (var node in paragraph.Descendants())
        {
            if (node.Name == W + "t")
            {
                text.Append(node.Value);
            }
            else if (node.Name == W + "tab")
            {
                text.Append('\t');
            }
            else if (node.Name == W + "br" || node.Name == W + "cr")
            {
                text.Append('\n');
            }
        }

        // List items (bullets or numbering) keep a marker, so requirement lists stay readable.
        var isListItem = paragraph.Element(W + "pPr")?.Element(W + "numPr") is not null;
        return isListItem ? "- " + text : text.ToString();
    }

    private static void AppendTable(StringBuilder text, XElement table)
    {
        foreach (var row in table.Elements(W + "tr"))
        {
            var cells = row.Elements(W + "tc")
                .Select(cell => string.Join(" ", cell.Descendants(W + "p").Select(Paragraph)).Trim());
            text.AppendLine(string.Join(" | ", cells));
        }
    }
}
