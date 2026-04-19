using WinUiFileManager.Domain.Enums;

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
            return Empty($"{selectedEntries.Count} items selected.", refreshVersion);
        }

        var entry = selectedEntries[0];
        if (entry.IsParentEntry)
        {
            return Empty(string.Empty, refreshVersion);
        }

        return new FileInspectorSelection(
            HasItem: true,
            StatusMessage: string.Empty,
            CanLoadDeferred: true,
            RefreshVersion: refreshVersion,
            FullPath: entry.Model.FullPath.DisplayPath,
            Name: entry.Name,
            Extension: entry.Extension,
            Kind: entry.Kind,
            SizeBytes: entry.SizeBytes,
            CreationTimeUtc: entry.CreationTimeUtc,
            LastWriteTimeUtc: entry.LastWriteTimeUtc,
            Attributes: entry.Attributes,
            AttributesFlags: entry.Model.Attributes);
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
