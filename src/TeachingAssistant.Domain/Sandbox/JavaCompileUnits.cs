using System.Text.RegularExpressions;

namespace TeachingAssistant.Domain.Sandbox;

/// <summary>A Java source file of a submission: its path inside the submission and its text.</summary>
public record JavaSourceFile
{
    public required string Path { get; init; }
    public required string Text { get; init; }
}

/// <summary>Files that are compiled together: one program, or one task of a submission that holds several.</summary>
public record JavaCompileUnit
{
    /// <summary>The folder the program's packages start from, relative to the submission; "" for the top.</summary>
    public required string Root { get; init; }

    /// <summary>The paths of its .java files, as in the submission.</summary>
    public required IReadOnlyList<string> Files { get; init; }
}

/// <summary>
/// Decides which of a submission's Java files are compiled together. Students often zip several tasks side by side, each
/// with its own Main class and no package; compiling them all at once would fail with "duplicate class" although every
/// task compiles on its own. So files are grouped by the folder their packages start from: a file declaring
/// <c>package shop;</c> in <c>src/shop/</c> belongs to <c>src/</c>, a file without a package belongs to its own folder.
/// </summary>
public static partial class JavaCompileUnits
{
    public static IReadOnlyList<JavaCompileUnit> Plan(IEnumerable<JavaSourceFile> files)
    {
        return files
            .Where(f => f.Path.EndsWith(".java", StringComparison.OrdinalIgnoreCase))
            .GroupBy(f => RootOf(f.Path, PackageOf(f.Text)), StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new JavaCompileUnit
            {
                Root = g.Key,
                Files = g.Select(f => f.Path).Order(StringComparer.Ordinal).ToList(),
            })
            .ToList();
    }

    /// <summary>The package a file declares, or null for the default package. Comments before it are ignored.</summary>
    public static string? PackageOf(string text)
    {
        var code = Comments().Replace(text, " ");
        var match = PackageDeclaration().Match(code);
        return match.Success ? Whitespace().Replace(match.Groups[1].Value, "") : null;
    }

    /// <summary>
    /// The folder a file's packages start from: its folder without the package's folders at the end. When the folders do
    /// not match the package (a student who moved files around), it is the file's own folder, where javac compiles it anyway.
    /// </summary>
    public static string RootOf(string path, string? package)
    {
        var normalized = path.Replace('\\', '/');
        var slash = normalized.LastIndexOf('/');
        var folder = slash < 0 ? "" : normalized[..slash];
        if (package is null)
        {
            return folder;
        }

        var packageFolders = package.Replace('.', '/');
        if (folder == packageFolders)
        {
            return "";
        }

        return folder.EndsWith("/" + packageFolders, StringComparison.Ordinal)
            ? folder[..^(packageFolders.Length + 1)]
            : folder;
    }

    [GeneratedRegex(@"/\*.*?\*/|//[^\n]*", RegexOptions.Singleline)]
    private static partial Regex Comments();

    [GeneratedRegex(@"^\s*package\s+([\p{L}_$][\p{L}\p{N}_$]*(?:\s*\.\s*[\p{L}_$][\p{L}\p{N}_$]*)*)\s*;", RegexOptions.Multiline)]
    private static partial Regex PackageDeclaration();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
