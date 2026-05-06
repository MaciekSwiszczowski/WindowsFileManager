namespace WinUiFileManager.Presentation.ViewModels.FileInspector;

internal sealed class StreamsFileInspectorCategory : IFileInspectorCategoryProvider
{
    public string Category => "Streams";

    public IReadOnlyList<FileInspectorFieldDefinition> Fields { get; } =
    [
        new("Streams", "Alternate Stream Count", "How many alternate data streams the item has.", 0),
        new("Streams", "Alternate Streams", "Names and sizes of alternate data streams.", 1)
    ];
}
