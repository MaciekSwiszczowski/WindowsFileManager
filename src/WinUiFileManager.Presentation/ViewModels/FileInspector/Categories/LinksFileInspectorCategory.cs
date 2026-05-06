namespace WinUiFileManager.Presentation.ViewModels.FileInspector;

internal sealed class LinksFileInspectorCategory : IFileInspectorCategoryProvider
{
    public string Category => "Links";

    public IReadOnlyList<FileInspectorFieldDefinition> Fields { get; } =
    [
        new("Links", "Link Target", "Target path of a symbolic link, junction, or shell shortcut.", 0),
        new("Links", "Link Status", "What kind of link Windows reports for the item.", 1),
        new("Links", "Reparse Tag", "Reparse point classification reported by Windows.", 2),
        new("Links", "Reparse Data", "Additional reparse data, when Windows can provide it.", 3),
        new("Links", "Object ID", "NTFS object identifier, when available.", 4)
    ];
}
