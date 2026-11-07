using System.Text;
using Microsoft.Extensions.Options;
using TeachingAssistant.Infrastructure.Storage;

namespace TeachingAssistant.Tests.Infrastructure;

// Works on real files in a temporary folder: restoring is only correct if the right file lands on disk.
public sealed class FileSystemSnapshotStorageTests : IDisposable
{
    private const string VersionPath = "instructions/Java II/Homework 2 (131641)/v1";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "ta-storage-" + Guid.NewGuid().ToString("N"));
    private readonly FileSystemSnapshotStorage _storage;

    public FileSystemSnapshotStorageTests()
    {
        _storage = new FileSystemSnapshotStorage(Options.Create(new StorageOptions { Root = _root }));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task RestoreOriginal_WritesTheFileUnderItsRecordedName_EvenWhenTheWholeFolderIsGone()
    {
        Assert.False(_storage.OriginalExists(VersionPath, "Homework_2.docx"));

        await _storage.RestoreOriginalAsync(VersionPath, "Homework_2.docx",
            stream => stream.WriteAsync("content"u8.ToArray()).AsTask(), TestContext.Current.CancellationToken);

        Assert.True(_storage.OriginalExists(VersionPath, "Homework_2.docx"));
        await using var opened = _storage.OpenOriginal(VersionPath, "Homework_2.docx")!;
        Assert.Equal("content", await new StreamReader(opened, Encoding.UTF8).ReadToEndAsync(TestContext.Current.CancellationToken));
        // No temporary file is left next to it.
        Assert.Equal(["Homework_2.docx"], Directory.GetFiles(Path.Combine(_root, VersionPath, "original")).Select(Path.GetFileName));
    }

    [Fact]
    public async Task RestoreOriginal_KeepsTheOldFile_WhenTheDownloadFails()
    {
        await _storage.RestoreOriginalAsync(VersionPath, "a.docx", s => s.WriteAsync("old"u8.ToArray()).AsTask(),
            TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<IOException>(() => _storage.RestoreOriginalAsync(VersionPath, "a.docx", async s =>
        {
            await s.WriteAsync("half"u8.ToArray());
            throw new IOException("network dropped");
        }, TestContext.Current.CancellationToken));

        await using var opened = _storage.OpenOriginal(VersionPath, "a.docx")!;
        Assert.Equal("old", await new StreamReader(opened).ReadToEndAsync(TestContext.Current.CancellationToken));
        Assert.Single(Directory.GetFiles(Path.Combine(_root, VersionPath, "original")));
    }

    [Fact]
    public async Task Paths_OutsideTheStorageRoot_AreNeverReadOrWritten()
    {
        Assert.False(_storage.OriginalExists("../../outside", "x.txt"));
        await Assert.ThrowsAsync<ArgumentException>(() => _storage.RestoreOriginalAsync("../../outside", "x.txt",
            _ => Task.CompletedTask, TestContext.Current.CancellationToken));
    }
}
