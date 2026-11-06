using TeachingAssistant.Application.Models;

namespace TeachingAssistant.Application.Interfaces;

/// <summary>
/// Reads documents the model cannot take as files (Word .docx), so they can be sent as text plus images instead.
/// Teachers' instruction files in Classroom are usually Word documents, often with code pasted as screenshots.
/// </summary>
public interface IDocumentReader
{
    /// <summary>True when this reader understands the file, judged by its name and type.</summary>
    bool CanRead(string fileName, string? mediaType);

    /// <summary>
    /// The document's text (paragraphs on their own lines, table rows with cells separated by " | ") and the images
    /// embedded in it, each marked in the text where it appears. Null when the file is damaged or not really such a document.
    /// </summary>
    ReadDocument? Read(byte[] content);
}
