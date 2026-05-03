namespace WinUiFileManager.Presentation.FileEntryTable;

public sealed class SpecFileEntryViewModel
{
    private static readonly string[] SizeSuffixes = ["B", "KB", "MB", "GB", "TB"];

    public SpecFileEntryViewModel(FileSystemEntryModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        Model = model;
        EntryKind = model.Kind == ItemKind.Directory ? FileEntryKind.Folder : FileEntryKind.File;
        Name = model.Name;
        Extension = model.Extension;
        Size = model.Kind == ItemKind.Directory ? string.Empty : FormatSize(model.Size);
        Modified = model.LastWriteTimeUtc.ToLocalTime();
        Attributes = model.Attributes.ToString();
    }

    public SpecFileEntryViewModel()
    {
        Model = null;
        EntryKind = FileEntryKind.Folder;
        Name = "..";
        Extension = string.Empty;
        Size = string.Empty;
        Modified = DateTime.MinValue;
        Attributes = string.Empty;
    }

    public static SpecFileEntryViewModel CreateParentEntry() => new();

    public static bool IsParentEntry(SpecFileEntryViewModel item) => item.Model is null;

    public FileSystemEntryModel? Model { get; }

    public FileEntryKind EntryKind { get; }

    public string Name { get; }

    public string Extension { get; }

    public string Size { get; }

    public DateTime Modified { get; }

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
