namespace TeachingAssistant.Application.Interfaces;

/// <summary>
/// Reads the text of documents the model cannot take as files (Word .docx), so they can be sent as text instead.
/// Teachers' instruction files in Classroom are usually Word documents.
/// </summary>
public interface IDocumentReader
{
    /// <summary>True when this reader understands the file, judged by its name and type.</summary>
    bool CanRead(string fileName, string? mediaType);

    /// <summary>
    /// The document's text: paragraphs on their own lines, table rows with cells separated by " | ".
    /// Null when the file is damaged or not really such a document.
    /// </summary>
    string? ReadText(byte[] content);
}
