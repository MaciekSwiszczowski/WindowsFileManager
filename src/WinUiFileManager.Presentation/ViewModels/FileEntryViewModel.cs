using CommunityToolkit.Mvvm.ComponentModel;
using WinUiFileManager.Domain.Enums;
using WinUiFileManager.Domain.ValueObjects;

namespace WinUiFileManager.Presentation.ViewModels;

public sealed partial class FileEntryViewModel : ObservableObject
{
    private static readonly string[] SizeSuffixes = ["B", "KB", "MB", "GB", "TB"];

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    [ObservableProperty]
    public partial string EditBuffer { get; set; } = string.Empty;

    public FileEntryViewModel(FileSystemEntryModel model)
    {
        Model = model;
    }

    private FileEntryViewModel()
    {
        Model = null!;
        IsParentEntry = true;
    }

    public static FileEntryViewModel CreateParentEntry() => new();

    public FileSystemEntryModel Model { get; }

    public bool IsParentEntry { get; }

    public string Name => IsParentEntry ? ".." : Model.Name;

    public string Extension => IsParentEntry ? string.Empty : Model.Extension;

    public string Size => IsParentEntry ? string.Empty : (Model.Kind == ItemKind.Directory ? string.Empty : FormatSize(Model.Size));

    public string LastWriteTime => IsParentEntry ? string.Empty : Model.LastWriteTimeUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

    public string CreationTime => IsParentEntry ? string.Empty : Model.CreationTimeUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

    public string Attributes => IsParentEntry ? string.Empty : Model.Attributes.ToString();

    public ItemKind Kind => IsParentEntry ? ItemKind.Directory : Model.Kind;

    public string FullPath => IsParentEntry ? string.Empty : Model.FullPath.DisplayPath;

    public bool IsDirectory => IsParentEntry || Model.Kind == ItemKind.Directory;

    public long SizeBytes => IsParentEntry ? -1 : Model.Size;

    public DateTime LastWriteTimeUtc => IsParentEntry ? DateTime.MinValue : Model.LastWriteTimeUtc;

    public DateTime CreationTimeUtc => IsParentEntry ? DateTime.MinValue : Model.CreationTimeUtc;

    public string UniqueKey => IsParentEntry ? ".." : Model.FullPath.DisplayPath;

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
