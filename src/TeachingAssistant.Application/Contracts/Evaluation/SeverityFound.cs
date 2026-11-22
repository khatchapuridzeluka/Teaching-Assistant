using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Application.Contracts;

/// <summary>Your kept mistakes of one severity (as you filed it) and how many the model found.</summary>
public record SeverityFound(MistakeSeverity Severity, int TeacherMistakes, int Found);
