namespace TeachingAssistant.Application.Models;

public record ReadDocument(string Text, IReadOnlyList<DocumentImage> Images);

public record DocumentImage(string Name, string MediaType, byte[] Content);
