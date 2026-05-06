using static WinUiFileManager.Presentation.ViewModels.FileInspectorCategory;

namespace WinUiFileManager.Presentation.ViewModels.FileInspector.Categories;

internal sealed class IdentityFileInspectorCategory : IFileInspectorCategoryProvider
{
    public FileInspectorCategory Category => Ids;

    public IReadOnlyList<FileInspectorFieldDefinition> Fields { get; } =
    [
        new(Ids, "File ID", "128-bit NTFS identifier for the selected file system entry.", 0),
        new(Ids, "Volume Serial", "Volume serial number of the drive that contains the item.", 1),
        new(Ids, "File Index (64-bit)", "Older 64-bit file index from the legacy Windows API. Diagnostic/compatibility value only.", 2),
        new(Ids, "Hard Link Count", "How many hard links point to the same file record, when available.", 3),
        new(Ids, "Final Path", "The resolved final path reported by Windows.", 4)
    ];
}
