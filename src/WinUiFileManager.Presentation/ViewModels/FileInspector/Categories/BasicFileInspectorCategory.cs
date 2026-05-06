using static WinUiFileManager.Presentation.ViewModels.FileInspectorCategory;

namespace WinUiFileManager.Presentation.ViewModels.FileInspector.Categories;

internal sealed class BasicFileInspectorCategory : IFileInspectorCategoryProvider
{
    public FileInspectorCategory Category => Basic;

    public IReadOnlyList<FileInspectorFieldDefinition> Fields { get; } =
    [
        new(Basic, "Name", "File or folder name", 0, IsDeferred: false),
        new(Basic, "Full Path", "Full selected item path", 1, IsDeferred: false),
        new(Basic, "Type", "Item type", 2, IsDeferred: false),
        new(Basic, "Extension", "File extension", 3, IsDeferred: false),
        new(Basic, "Size", "Size in a human-readable format", 4, IsDeferred: false),
        new(Basic, "Attributes", "File system attributes", 5, IsDeferred: false)
    ];

    public static void ApplySelection(FileInspectorSelection selection, FileInspectorFieldStore fields)
    {
        fields.SetValue("Name", selection.Name);
        fields.SetValue("Full Path", selection.FullPath);
        fields.SetValue("Type", selection.Kind == ItemKind.Directory ? "Folder" : "File");
        fields.SetValue("Extension", selection.Extension);
        fields.SetValue("Size", FormatSize(selection.SizeBytes));
        fields.SetValue("Attributes", selection.Attributes);
    }

    private static string FormatSize(long sizeBytes)
    {
        if (sizeBytes < 0)
        {
            return string.Empty;
        }

        string[] suffixes = ["B", "KB", "MB", "GB", "TB"];
        var suffixIndex = 0;
        var size = (double)sizeBytes;

        while (size >= 1024 && suffixIndex < suffixes.Length - 1)
        {
            size /= 1024;
            suffixIndex++;
        }

        return suffixIndex == 0
            ? $"{size:F0} {suffixes[suffixIndex]}"
            : $"{size:F2} {suffixes[suffixIndex]}";
    }
}
