namespace TeachingAssistant.Application.Contracts;

public record ExportedFile(byte[] Content, string ContentType, string FileName);
