namespace WinUiFileManager.Presentation.ViewModels.FileInspector;

internal sealed class BasicFileInspectorCategory : IFileInspectorCategoryProvider
{
    public string Category => "Basic";

    public IReadOnlyList<FileInspectorFieldDefinition> Fields { get; } =
    [
        new("Basic", "Name", "File or folder name", 0, IsDeferred: false),
        new("Basic", "Full Path", "Full selected item path", 1, IsDeferred: false),
        new("Basic", "Type", "Item type", 2, IsDeferred: false),
        new("Basic", "Extension", "File extension", 3, IsDeferred: false),
        new("Basic", "Size", "Size in a human-readable format", 4, IsDeferred: false),
        new("Basic", "Attributes", "File system attributes", 5, IsDeferred: false)
    ];
}
