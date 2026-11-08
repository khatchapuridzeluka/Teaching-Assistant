namespace TeachingAssistant.Application.Models;

public record StoredFileLocation(string VersionPath, string StoredName, string Name, string? MimeType);

public record StoredFileContent(Stream Content, string ContentType, string FileName);
