namespace WinUiFileManager.Presentation.ViewModels.FileInspector;

internal sealed class IdentityFileInspectorCategory : IFileInspectorCategoryProvider
{
    public string Category => "IDs";

    public IReadOnlyList<FileInspectorFieldDefinition> Fields { get; } =
    [
        new("IDs", "File ID", "128-bit NTFS identifier for the selected file system entry.", 0),
        new("IDs", "Volume Serial", "Volume serial number of the drive that contains the item.", 1),
        new("IDs", "File Index (64-bit)", "Older 64-bit file index from the legacy Windows API. Diagnostic/compatibility value only.", 2),
        new("IDs", "Hard Link Count", "How many hard links point to the same file record, when available.", 3),
        new("IDs", "Final Path", "The resolved final path reported by Windows.", 4)
    ];
}
