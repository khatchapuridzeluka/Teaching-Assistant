using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Application.Contracts;

public record SeverityDecisions(MistakeSeverity Severity, int Kept, int Edited, int Rejected);
