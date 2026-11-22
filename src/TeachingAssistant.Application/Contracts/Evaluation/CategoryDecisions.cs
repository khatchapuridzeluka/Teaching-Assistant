using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Application.Contracts;

public record CategoryDecisions(MistakeCategory Category, int Kept, int Edited, int Rejected);
