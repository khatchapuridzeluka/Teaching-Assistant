namespace TeachingAssistant.Application.Models;

/// <summary>
/// Where one stored file is: its version's folder and its name inside original/.
/// Name is the student's original file name, used when the file is downloaded.
/// </summary>
public record StoredFileLocation(string VersionPath, string StoredName, string Name, string? MimeType);

/// <summary>
/// An open stored file, ready to be sent to the browser. The caller disposes the stream.
/// </summary>
public record StoredFileContent(Stream Content, string ContentType, string FileName);
