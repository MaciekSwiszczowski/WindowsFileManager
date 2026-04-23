using WinUiFileManager.Domain.Enums;
using WinUiFileManager.Domain.ValueObjects;

namespace WinUiFileManager.Presentation.ViewModels;

public sealed class FileEntryViewModel
{
    private static readonly string[] SizeSuffixes = ["B", "KB", "MB", "GB", "TB"];

    public FileEntryViewModel(FileSystemEntryModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        Model = model;
        EntryKind = model.Kind == ItemKind.Directory ? FileEntryKind.Folder : FileEntryKind.File;
        Name = model.Name;
        Extension = model.Extension;
        Size = model.Kind == ItemKind.Directory ? string.Empty : FormatSize(model.Size);
        LastWriteTime = model.LastWriteTimeUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
        Attributes = model.Attributes.ToString();
    }

    private FileEntryViewModel()
    {
        Model = null;
        EntryKind = FileEntryKind.Parent;
        Name = "..";
        Extension = string.Empty;
        Size = string.Empty;
        LastWriteTime = string.Empty;
        Attributes = string.Empty;
    }

    public static FileEntryViewModel CreateParentEntry() => new();

    public FileSystemEntryModel? Model { get; }

    public FileEntryKind EntryKind { get; }

    public string Name { get; }

    public string Extension { get; }

    public string Size { get; }

    public string LastWriteTime { get; }

    public string Attributes { get; }

    private static string FormatSize(long bytes)
    {
        var suffixIndex = 0;
        var size = (double)bytes;

        while (size >= 1024 && suffixIndex < SizeSuffixes.Length - 1)
        {
            size /= 1024;
            suffixIndex++;
        }

        return suffixIndex == 0
            ? $"{size:F0} {SizeSuffixes[suffixIndex]}"
            : $"{size:F2} {SizeSuffixes[suffixIndex]}";
    }
}
