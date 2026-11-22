namespace TeachingAssistant.Application.Models;

/// <summary>A file for the working folder.</summary>
public record SandboxFile
{
    /// <summary>Relative path with forward slashes, for example "src/shop/Main.java"; never absolute, never "..".</summary>
    public required string Path { get; init; }

    /// <summary>The file's bytes.</summary>
    public required byte[] Content { get; init; }
}
