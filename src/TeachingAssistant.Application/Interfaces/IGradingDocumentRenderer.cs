using TeachingAssistant.Application.Models;

namespace TeachingAssistant.Application.Interfaces;

public interface IGradingDocumentRenderer
{
    byte[] RenderPdf(GradingDocument document);
}
