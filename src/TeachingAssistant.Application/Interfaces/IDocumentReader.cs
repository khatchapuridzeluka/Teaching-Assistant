using TeachingAssistant.Application.Models;

namespace TeachingAssistant.Application.Interfaces;

public interface IDocumentReader
{
    bool CanRead(string fileName, string? mediaType);

    ReadDocument? Read(byte[] content);
}
