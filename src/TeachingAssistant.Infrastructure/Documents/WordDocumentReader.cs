using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Models;

namespace TeachingAssistant.Infrastructure.Documents;

public class WordDocumentReader : IDocumentReader
{
    private const string DocxMediaType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    private const long MaxXmlCharacters = 20_000_000;

    private const int MaxImageBytes = 5 * 1024 * 1024;
    private const int MaxImages = 30;

    private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace A = "http://schemas.openxmlformats.org/drawingml/2006/main";
    private static readonly XNamespace V = "urn:schemas-microsoft-com:vml";
    private static readonly XNamespace Mc = "http://schemas.openxmlformats.org/markup-compatibility/2006";
    private static readonly XNamespace Rels = "http://schemas.openxmlformats.org/package/2006/relationships";

    private static readonly Dictionary<string, string> ImageTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".gif"] = "image/gif",
        [".webp"] = "image/webp",
    };

    public bool CanRead(string fileName, string? mediaType) =>
        string.Equals(mediaType, DocxMediaType, StringComparison.OrdinalIgnoreCase)
        || fileName.EndsWith(".docx", StringComparison.OrdinalIgnoreCase);

    public ReadDocument? Read(byte[] content)
    {
        try
        {
            using var zip = new ZipArchive(new MemoryStream(content), ZipArchiveMode.Read);
            var body = LoadXml(zip, "word/document.xml")?.Root?.Element(W + "body");
            if (body is null)
            {
                return null;
            }

            var walker = new Walker(zip, ImageTargets(zip));
            foreach (var block in body.Elements())
            {
                if (block.Name == W + "p")
                {
                    walker.Text.AppendLine(walker.Paragraph(block));
                }
                else if (block.Name == W + "tbl")
                {
                    walker.Table(block);
                }
            }

            return new ReadDocument { Text = walker.Text.ToString().Trim(), Images = walker.Images };
        }
        catch (Exception ex) when (ex is InvalidDataException or XmlException)
        {
            return null;
        }
    }

    private static XDocument? LoadXml(ZipArchive zip, string path)
    {
        var entry = zip.GetEntry(path);
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
        return XDocument.Load(reader);
    }

    private static Dictionary<string, string> ImageTargets(ZipArchive zip)
    {
        var targets = new Dictionary<string, string>();
        var relationships = LoadXml(zip, "word/_rels/document.xml.rels")?.Root?.Elements(Rels + "Relationship") ?? [];
        foreach (var relationship in relationships)
        {
            if ((string?)relationship.Attribute("TargetMode") == "External"
                || ((string?)relationship.Attribute("Type"))?.EndsWith("/image", StringComparison.Ordinal) != true
                || (string?)relationship.Attribute("Id") is not { } id
                || (string?)relationship.Attribute("Target") is not { } target)
            {
                continue;
            }

            targets[id] = ZipPath(target);
        }

        return targets;
    }

    private static string ZipPath(string target)
    {
        var path = target.StartsWith('/') ? target.TrimStart('/') : "word/" + target;
        var segments = new List<string>();
        foreach (var segment in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == "..")
            {
                if (segments.Count > 0)
                {
                    segments.RemoveAt(segments.Count - 1);
                }
            }
            else if (segment != ".")
            {
                segments.Add(segment);
            }
        }

        return string.Join('/', segments);
    }

    private sealed class Walker(ZipArchive zip, Dictionary<string, string> imageTargets)
    {
        private readonly Dictionary<string, int> _numbers = [];

        public StringBuilder Text { get; } = new();
        public List<DocumentImage> Images { get; } = [];

        public string Paragraph(XElement paragraph)
        {
            var text = new StringBuilder();
            foreach (var node in paragraph.Descendants())
            {
                if (node.Ancestors(Mc + "Fallback").Any())
                {
                    continue;
                }

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
                else if (node.Name == A + "blip" && (string?)node.Attribute(R + "embed") is { } embedded)
                {
                    text.Append(Image(embedded));
                }
                else if (node.Name == V + "imagedata" && (string?)node.Attribute(R + "id") is { } legacy)
                {
                    text.Append(Image(legacy));
                }
            }

            var isListItem = paragraph.Element(W + "pPr")?.Element(W + "numPr") is not null;
            return isListItem ? "- " + text : text.ToString();
        }

        public void Table(XElement table)
        {
            foreach (var row in table.Elements(W + "tr"))
            {
                var cells = row.Elements(W + "tc")
                    .Select(cell => string.Join(" ", cell.Descendants(W + "p").Select(Paragraph)).Trim());
                Text.AppendLine(string.Join(" | ", cells));
            }
        }

        private string Image(string relationshipId)
        {
            if (_numbers.TryGetValue(relationshipId, out var known))
            {
                return $"[image {known}]";
            }

            if (!imageTargets.TryGetValue(relationshipId, out var path) || zip.GetEntry(path) is not { } entry)
            {
                return "";
            }

            if (!ImageTypes.TryGetValue(Path.GetExtension(path), out var mediaType))
            {
                return $"[an image in {Path.GetExtension(path)} format that could not be included]";
            }

            if (Images.Count >= MaxImages)
            {
                return "[an image that could not be included: too many images]";
            }

            var content = ReadLimited(entry);
            if (content is null)
            {
                return "[an image that could not be included: too large]";
            }

            Images.Add(new DocumentImage { Name = Path.GetFileName(path), MediaType = mediaType, Content = content });
            _numbers[relationshipId] = Images.Count;
            return $"[image {Images.Count}]";
        }

        private static byte[]? ReadLimited(ZipArchiveEntry entry)
        {
            using var stream = entry.Open();
            using var buffer = new MemoryStream();
            var chunk = new byte[81920];
            int read;
            while ((read = stream.Read(chunk, 0, chunk.Length)) > 0)
            {
                if (buffer.Length + read > MaxImageBytes)
                {
                    return null;
                }

                buffer.Write(chunk, 0, read);
            }

            return buffer.ToArray();
        }
    }
}
