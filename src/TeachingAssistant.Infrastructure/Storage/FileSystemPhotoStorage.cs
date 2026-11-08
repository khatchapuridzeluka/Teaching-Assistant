using Microsoft.Extensions.Options;
using TeachingAssistant.Application.Interfaces;

namespace TeachingAssistant.Infrastructure.Storage;

public class FileSystemPhotoStorage(IOptions<StorageOptions> options) : IStudentPhotoStorage
{
    private const string Folder = "students";

    private static readonly Dictionary<string, string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/webp"] = ".webp",
        ["image/gif"] = ".gif",
    };

    private readonly string _root = options.Value.Root;

    public async Task<string> SaveAsync(string externalUserId, DownloadedPhoto photo, CancellationToken cancellationToken = default)
    {
        var id = new string(externalUserId.Where(char.IsLetterOrDigit).ToArray());
        if (id.Length == 0)
        {
            throw new ArgumentException("The user ID has no usable characters.", nameof(externalUserId));
        }

        var storedName = id + Extensions.GetValueOrDefault(photo.ContentType, ".img");
        var folder = Path.Combine(_root, Folder);
        Directory.CreateDirectory(folder);

        foreach (var old in Directory.EnumerateFiles(folder, id + ".*").Where(f => Path.GetFileName(f) != storedName))
        {
            File.Delete(old);
        }

        var path = Path.Combine(folder, storedName);
        var temp = $"{path}.{Guid.NewGuid():N}.part";
        try
        {
            await File.WriteAllBytesAsync(temp, photo.Bytes, cancellationToken);
            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp))
            {
                File.Delete(temp);
            }
        }

        return storedName;
    }

    public Stream? Open(string storedName)
    {
        var path = PathOf(storedName);
        return path is null || !File.Exists(path)
            ? null
            : new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 16384, useAsync: true);
    }

    public void Delete(string storedName)
    {
        var path = PathOf(storedName);
        if (path is not null && File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private string? PathOf(string storedName) =>
        storedName.Length == 0 || storedName != Path.GetFileName(storedName) ? null : Path.Combine(_root, Folder, storedName);
}
