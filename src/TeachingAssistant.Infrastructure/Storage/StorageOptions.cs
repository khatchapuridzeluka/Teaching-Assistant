namespace TeachingAssistant.Infrastructure.Storage;

public class StorageOptions
{
    public const string SectionName = "Storage";

    public string Root { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TeachingAssistant");
}
