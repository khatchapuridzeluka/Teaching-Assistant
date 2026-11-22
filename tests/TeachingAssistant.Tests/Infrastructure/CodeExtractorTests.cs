using System.IO.Compression;
using System.Text;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Domain.Ingestion;
using TeachingAssistant.Infrastructure.Storage;

namespace TeachingAssistant.Tests.Infrastructure;

// Works on real files in a temporary folder, because the extractor's job is exactly what lands on disk.
public sealed class CodeExtractorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ta-extract-" + Guid.NewGuid().ToString("N"));
    private string Original => Path.Combine(_root, "original");
    private string Code => Path.Combine(_root, "code");

    public CodeExtractorTests() => Directory.CreateDirectory(Original);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Extract_UnpacksCode_AndLeavesOutJunk()
    {
        Zip("hw.zip", ("src/Main.java", "class Main {}"), ("src/Book.java", "class Book {}"),
            ("__MACOSX/src/._Main.java", "x"), (".idea/workspace.xml", "x"), ("out/Main.class", "x"));

        var result = Extract(Archive("hw.zip"));

        Assert.Equal(["src/Book.java", "src/Main.java"], Files());
        Assert.Equal(2, result.FileCount);
        Assert.NotNull(result.ContentHash);
        Assert.Equal(["hw.zip"], result.ExtractedStoredNames);
        Assert.Empty(result.Problems);
    }

    [Fact]
    public void Extract_KeepsFoldersOfAWindowsArchive_ThatSeparatesThemWithBackslashes()
    {
        Zip("hw.zip", (@"3D Figures\src\Main.java", "class Main {}"), (@"3D Figures\.idea\misc.xml", "x"),
            (@"3D Figures\out\production\Main.class", "x"), (@"MysticCreatures\src\Main.java", "class Main {}"));

        Extract(Archive("hw.zip"));

        Assert.Equal(["3D Figures/src/Main.java", "MysticCreatures/src/Main.java"], Files());
    }

    [Fact]
    public void Extract_UnpacksADownloadedRepositoryLikeAnArchive()
    {
        Zip("library-1111111.zip", ("ana-library-1111111/src/Main.java", "class Main {}"), ("ana-library-1111111/.git/config", "x"));

        var result = Extract(new ExtractableFile { StoredName = "library-1111111.zip", Kind = ContentKind.Repository });

        Assert.Equal(["ana-library-1111111/src/Main.java"], Files());
        Assert.Equal(["library-1111111.zip"], result.ExtractedStoredNames);
    }

    [Fact]
    public void Extract_NeverWritesOutsideTheCodeFolder()
    {
        Zip("evil.zip", ("../../escaped.txt", "x"), ("ok/Main.java", "class Main {}"));

        Extract(Archive("evil.zip"));

        Assert.False(File.Exists(Path.Combine(_root, "escaped.txt")));
        Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(_root)!, "escaped.txt")));
        Assert.Equal(["ok/Main.java"], Files());
    }

    [Fact]
    public void Extract_UnpacksAZipInsideAZip()
    {
        var inner = ZipBytes(("Main.java", "class Main {}"));
        Zip("outer.zip", ("project.zip", inner));

        Extract(Archive("outer.zip"));

        Assert.Equal(["project/Main.java"], Files());
    }

    [Fact]
    public void Extract_ReportsABrokenArchive_AndKeepsTheOthers()
    {
        File.WriteAllText(Path.Combine(Original, "broken.rar"), "this is not an archive");
        Zip("good.zip", ("Main.java", "class Main {}"));

        var result = Extract(Archive("broken.rar"), Archive("good.zip"));

        Assert.Equal(["good/Main.java"], Files());
        Assert.Equal(["good.zip"], result.ExtractedStoredNames);
        Assert.Contains(result.Problems, p => p.Contains("broken.rar"));
    }

    [Fact]
    public void Extract_CopiesLooseSourceFiles_AndIgnoresDocuments()
    {
        File.WriteAllText(Path.Combine(Original, "Main.java"), "class Main {}");
        File.WriteAllText(Path.Combine(Original, "report.pdf"), "%PDF");

        var result = Extract(new ExtractableFile { StoredName = "Main.java", Kind = ContentKind.SourceFile }, new ExtractableFile
        {
            StoredName = "report.pdf",
            Kind = ContentKind.Document,
        });

        Assert.Equal(["Main.java"], Files());
        Assert.Equal(1, result.FileCount);
    }

    [Fact]
    public void ContentHash_IsTheSame_HoweverTheCodeWasZipped()
    {
        Zip("a.zip", ("Main.java", "class Main {}"), ("Book.java", "class Book {}"));
        var first = Extract(Archive("a.zip")).ContentHash;

        Directory.Delete(Code, recursive: true);
        Zip("b.zip", ("Book.java", "class Book {}"), ("Main.java", "class Main {}")); // other order, other archive name
        var second = Extract(Archive("b.zip")).ContentHash;

        Assert.Equal(first, second);
    }

    [Fact]
    public void Extract_WithNothingToExtract_HasNoHash()
    {
        Zip("empty.zip", (".idea/misc.xml", "x"));

        var result = Extract(Archive("empty.zip"));

        Assert.Null(result.ContentHash);
        Assert.Contains(result.Problems, p => p.Contains("No code files"));
    }

    private CodeExtraction Extract(params ExtractableFile[] files) =>
        CodeExtractor.Extract(Original, Code, files, TestContext.Current.CancellationToken);

    private static ExtractableFile Archive(string name) => new() { StoredName = name, Kind = ContentKind.CodeArchive };

    private List<string> Files() => CodeExtractor.List(Code).Select(f => f.Path).ToList();

    private void Zip(string name, params (string Path, string Content)[] entries) =>
        File.WriteAllBytes(Path.Combine(Original, name), ZipBytes(entries.Select(e => (e.Path, Encoding.UTF8.GetBytes(e.Content))).ToArray()));

    private void Zip(string name, (string Path, byte[] Content) entry) =>
        File.WriteAllBytes(Path.Combine(Original, name), ZipBytes(entry));

    private static byte[] ZipBytes(params (string Path, string Content)[] entries) =>
        ZipBytes(entries.Select(e => (e.Path, Encoding.UTF8.GetBytes(e.Content))).ToArray());

    private static byte[] ZipBytes(params (string Path, byte[] Content)[] entries)
    {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (path, content) in entries)
            {
                using var stream = zip.CreateEntry(path).Open();
                stream.Write(content);
            }
        }
        return buffer.ToArray();
    }
}
