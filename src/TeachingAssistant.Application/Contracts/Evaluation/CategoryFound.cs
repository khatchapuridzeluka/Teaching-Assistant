using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Application.Contracts;

/// <summary>Your kept mistakes of one category (as you filed it) and how many the model found.</summary>
public record CategoryFound(MistakeCategory Category, int TeacherMistakes, int Found);
