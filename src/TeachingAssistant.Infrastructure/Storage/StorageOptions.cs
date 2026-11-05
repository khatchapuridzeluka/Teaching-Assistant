namespace TeachingAssistant.Infrastructure.Storage;

/// <summary>
/// Settings bound from the "Storage" configuration section.
/// </summary>
public class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>
    /// Folder holding submission files. Defaults to %LOCALAPPDATA%\TeachingAssistant: local, outside the repository,
    /// and not synced by OneDrive (student work must not be uploaded anywhere).
    /// </summary>
    public string Root { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TeachingAssistant");
}
