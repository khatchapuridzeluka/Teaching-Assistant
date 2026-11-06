namespace TeachingAssistant.Application.Models;

/// <summary>
/// A document read for the model: its text, and the images embedded in it (code screenshots, diagrams). Each image is
/// marked in the text where it appears as "[image N]", and Images[N - 1] is that image, so the model can tell which
/// picture belongs to which task.
/// </summary>
public record ReadDocument(string Text, IReadOnlyList<DocumentImage> Images);

/// <summary>An image embedded in a document, in a format the model can read (MediaType image/png, image/jpeg, …).</summary>
public record DocumentImage(string Name, string MediaType, byte[] Content);
