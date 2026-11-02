namespace TeachingAssistant.Application.Contracts;

/// <summary>
/// A course the teacher teaches. State is the source's own state, e.g. ACTIVE or ARCHIVED for Classroom.
/// Link opens the course in its source (Classroom) when available.
/// </summary>
public record CourseResponse(
    string Id,
    string Name,
    string? Section,
    string State,
    string? Link);
