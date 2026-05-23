using WinUiFileManager.Presentation.FileEntryTable;

namespace WinUiFileManager.Presentation.ViewModels;

public sealed record FileInspectorSelection(
    bool HasItem,
    bool CanLoadDeferred,
    long RefreshVersion,
    string FullPath,
    string Name,
    string Extension,
    ItemKind Kind,
    long SizeBytes,
    DateTime CreationTime,
    DateTime LastWriteTime,
    string Attributes,
    FileAttributes AttributesFlags)
{
    public static FileInspectorSelection FromSelection(
        IReadOnlyList<SpecFileEntryViewModel> selectedEntries,
        long refreshVersion)
    {
        if (selectedEntries.Count != 1)
        {
            return Empty(refreshVersion);
        }

        var entry = selectedEntries[0];
        if (entry.Model is not { } model)
        {
            return Empty(refreshVersion);
        }

        return new FileInspectorSelection(
            HasItem: true,
            CanLoadDeferred: true,
            RefreshVersion: refreshVersion,
            FullPath: model.FullPath.DisplayPath,
            Name: model.Name,
            Extension: model.Extension,
            Kind: model.Kind,
            SizeBytes: model.Size ?? -1,
            CreationTime: model.CreationTime,
            LastWriteTime: model.LastWriteTime,
            Attributes: model.Attributes.ToString(),
            AttributesFlags: model.Attributes);
    }

    public static FileInspectorSelection NoSelection(long refreshVersion) =>
        Empty(refreshVersion);

    private static FileInspectorSelection Empty(long refreshVersion) =>
        new(
            HasItem: false,
            CanLoadDeferred: false,
            RefreshVersion: refreshVersion,
            FullPath: string.Empty,
            Name: string.Empty,
            Extension: string.Empty,
            Kind: ItemKind.File,
            SizeBytes: -1,
            CreationTime: DateTime.MinValue,
            LastWriteTime: DateTime.MinValue,
            Attributes: string.Empty,
            AttributesFlags: FileAttributes.None);
}
