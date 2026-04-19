using WinUiFileManager.Domain.Enums;

namespace WinUiFileManager.Presentation.ViewModels;

public sealed record FileInspectorSelection(
    bool HasItem,
    string StatusMessage,
    bool CanLoadDeferred,
    string FullPath,
    string Name,
    string Extension,
    ItemKind Kind,
    long SizeBytes,
    DateTime CreationTimeUtc,
    DateTime LastWriteTimeUtc,
    string Attributes)
{
    public static FileInspectorSelection FromSelection(
        IReadOnlyList<FileEntryViewModel> selectedEntries,
        bool isPaneLoading)
    {
        if (isPaneLoading)
        {
            return Empty("Pane is loading...");
        }

        if (selectedEntries.Count == 0)
        {
            return Empty(string.Empty);
        }

        if (selectedEntries.Count != 1)
        {
            return Empty($"{selectedEntries.Count} items selected.");
        }

        var entry = selectedEntries[0];
        if (entry.IsParentEntry)
        {
            return Empty(string.Empty);
        }

        return new FileInspectorSelection(
            HasItem: true,
            StatusMessage: string.Empty,
            CanLoadDeferred: true,
            FullPath: entry.Model.FullPath.DisplayPath,
            Name: entry.Name,
            Extension: entry.Extension,
            Kind: entry.Kind,
            SizeBytes: entry.SizeBytes,
            CreationTimeUtc: entry.CreationTimeUtc,
            LastWriteTimeUtc: entry.LastWriteTimeUtc,
            Attributes: entry.Attributes);
    }

    private static FileInspectorSelection Empty(string statusMessage)
    {
        return new FileInspectorSelection(
            HasItem: false,
            StatusMessage: statusMessage,
            CanLoadDeferred: false,
            FullPath: string.Empty,
            Name: string.Empty,
            Extension: string.Empty,
            Kind: ItemKind.File,
            SizeBytes: -1,
            CreationTimeUtc: DateTime.MinValue,
            LastWriteTimeUtc: DateTime.MinValue,
            Attributes: string.Empty);
    }
}
