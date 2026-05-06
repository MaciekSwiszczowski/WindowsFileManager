namespace WinUiFileManager.Presentation.ViewModels.FileInspector;

internal sealed class ThumbnailsFileInspectorCategory : IFileInspectorCategoryProvider
{
    public string Category => "Thumbnails";

    public IReadOnlyList<FileInspectorFieldDefinition> Fields { get; } =
    [
        new("Thumbnails", "Thumbnail", "Thumbnail preview reported by Windows, when available.", 0),
        new("Thumbnails", "Has Thumbnail", "Whether Windows could provide a thumbnail for the selected item.", 1),
        new("Thumbnails", "Association", "Shell association or file type hint used for the thumbnail, when available.", 2)
    ];
}
