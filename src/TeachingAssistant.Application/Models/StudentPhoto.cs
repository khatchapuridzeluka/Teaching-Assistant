namespace TeachingAssistant.Application.Models;

/// <summary>A stored profile photo to send to the browser; Hash (SHA-256 of the bytes) serves as its ETag.</summary>
public record StudentPhoto(Stream Content, string ContentType, string Hash);
