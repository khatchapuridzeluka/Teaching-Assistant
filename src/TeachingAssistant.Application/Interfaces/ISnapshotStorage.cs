namespace TeachingAssistant.Application.Interfaces;

/// <summary>
/// Where the files of each submission version are kept (local disk now, cloud storage later).
/// </summary>
public interface ISnapshotStorage
{
    /// <summary>
    /// Returns the folder for a new submission, relative to the storage root, readable when browsing
    /// (course / assignment / student). The student ID keeps two students with the same name apart.
    /// Only builds the name; nothing is created until a version is committed.
    /// </summary>
    string GetSubmissionFolder(string courseName, string assignmentTitle, string studentName, string studentId);

    /// <summary>
    /// Starts writing a new version's files under the submission's folder. Nothing is visible at the version's path
    /// until the writer is committed; disposing an uncommitted writer discards everything written so far.
    /// </summary>
    ISnapshotWriter BeginVersion(string submissionFolder, int versionNumber);
}

/// <summary>
/// Collects one version's files in a private location until <see cref="CommitAsync"/> makes them visible all at once.
/// </summary>
public interface ISnapshotWriter : IAsyncDisposable
{
    /// <summary>The version's path relative to the storage root, as stored in the database.</summary>
    string RelativePath { get; }

    /// <summary>
    /// Adds a file to the version's original/ folder. The name is made safe and unique;
    /// the stored name is returned. <paramref name="write"/> fills the file's content.
    /// </summary>
    Task<string> AddOriginalAsync(string fileName, Func<Stream, Task> write, CancellationToken cancellationToken = default);

    /// <summary>Makes the version's files visible at <see cref="RelativePath"/>.</summary>
    Task CommitAsync(CancellationToken cancellationToken = default);
}
