namespace TeachingAssistant.Application.Contracts;

/// <summary>Start an evaluation run: grade the approved reports again with these models, in one course or all (CourseId null).</summary>
public record StartEvaluationRunRequest(IReadOnlyList<string> Models, string? CourseId = null);
