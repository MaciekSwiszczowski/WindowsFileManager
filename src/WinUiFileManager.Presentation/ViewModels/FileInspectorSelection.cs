using WinUiFileManager.Presentation.FileEntryTable;

namespace WinUiFileManager.Presentation.ViewModels;

public sealed record FileInspectorSelection(
    bool HasItem,
    string StatusMessage,
    bool CanLoadDeferred,
    long RefreshVersion,
    string FullPath,
    string Name,
    string Extension,
    ItemKind Kind,
    long SizeBytes,
    DateTime CreationTimeUtc,
    DateTime LastWriteTimeUtc,
    string Attributes,
    FileAttributes AttributesFlags)
{
    public static FileInspectorSelection FromSelection(
        IReadOnlyList<FileEntryViewModel> selectedEntries,
        bool isPaneLoading,
        long refreshVersion)
    {
        if (isPaneLoading)
        {
            return Empty("Pane is loading...", refreshVersion);
        }

        if (selectedEntries.Count == 0)
        {
            return Empty(string.Empty, refreshVersion);
        }

        if (selectedEntries.Count != 1)
        {
            return Empty(string.Empty, refreshVersion);
        }

        var entry = selectedEntries[0];
        if (entry.Model is not { } model)
        {
            return Empty(string.Empty, refreshVersion);
        }

        return new FileInspectorSelection(
            HasItem: true,
            StatusMessage: string.Empty,
            CanLoadDeferred: true,
            RefreshVersion: refreshVersion,
            FullPath: model.FullPath.DisplayPath,
            Name: entry.Name,
            Extension: entry.Extension,
            Kind: model.Kind,
            SizeBytes: model.Size,
            CreationTimeUtc: model.CreationTimeUtc,
            LastWriteTimeUtc: model.LastWriteTimeUtc,
            Attributes: entry.Attributes,
            AttributesFlags: model.Attributes);
    }

    public static FileInspectorSelection FromSelection(
        IReadOnlyList<SpecFileEntryViewModel> selectedEntries,
        long refreshVersion)
    {
        if (selectedEntries.Count != 1)
        {
            return Empty(string.Empty, refreshVersion);
        }

        var entry = selectedEntries[0];
        if (entry.Model is not { } model)
        {
            return Empty(string.Empty, refreshVersion);
        }

        return new FileInspectorSelection(
            HasItem: true,
            StatusMessage: string.Empty,
            CanLoadDeferred: true,
            RefreshVersion: refreshVersion,
            FullPath: model.FullPath.DisplayPath,
            Name: entry.Name,
            Extension: entry.Extension,
            Kind: model.Kind,
            SizeBytes: model.Size,
            CreationTimeUtc: model.CreationTimeUtc,
            LastWriteTimeUtc: model.LastWriteTimeUtc,
            Attributes: entry.Attributes,
            AttributesFlags: model.Attributes);
    }

    private static FileInspectorSelection Empty(string statusMessage, long refreshVersion)
    {
        return new FileInspectorSelection(
            HasItem: false,
            StatusMessage: statusMessage,
            CanLoadDeferred: false,
            RefreshVersion: refreshVersion,
            FullPath: string.Empty,
            Name: string.Empty,
            Extension: string.Empty,
            Kind: ItemKind.File,
            SizeBytes: -1,
            CreationTimeUtc: DateTime.MinValue,
            LastWriteTimeUtc: DateTime.MinValue,
            Attributes: string.Empty,
            AttributesFlags: FileAttributes.None);
    }
}
