namespace TeachingAssistant.Application.Models;

/// <summary>
/// A student on a course's roster in the source: the source's user ID, the name it shows, and the URL of their profile
/// photo (null when they have none or only the default picture).
/// </summary>
public record RosterStudent(string Id, string Name, string? PhotoUrl = null);
