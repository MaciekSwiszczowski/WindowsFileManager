using WinUiFileManager.Domain.Enums;

namespace WinUiFileManager.Presentation.ViewModels;

public sealed class FileEntryComparer : IComparer<FileEntryViewModel>
{
    private readonly SortColumn _column;
    private readonly bool _ascending;

    public FileEntryComparer(SortColumn column, bool ascending)
    {
        _column = column;
        _ascending = ascending;
    }

    public int Compare(FileEntryViewModel? x, FileEntryViewModel? y)
    {
        if (ReferenceEquals(x, y)) return 0;
        if (x is null) return 1;
        if (y is null) return -1;

        if (x.IsParentEntry && !y.IsParentEntry) return -1;
        if (!x.IsParentEntry && y.IsParentEntry) return 1;
        if (x.IsParentEntry && y.IsParentEntry) return 0;

        if (x.IsDirectory && !y.IsDirectory) return -1;
        if (!x.IsDirectory && y.IsDirectory) return 1;

        var result = CompareByColumn(x, y);
        return _ascending ? result : -result;
    }

    private int CompareByColumn(FileEntryViewModel x, FileEntryViewModel y) => _column switch
    {
        SortColumn.Name => string.Compare(x.Name, y.Name, StringComparison.OrdinalIgnoreCase),
        SortColumn.Extension => string.Compare(x.Extension, y.Extension, StringComparison.OrdinalIgnoreCase),
        SortColumn.Size => x.SizeBytes.CompareTo(y.SizeBytes),
        SortColumn.Modified => x.LastWriteTimeUtc.CompareTo(y.LastWriteTimeUtc),
        SortColumn.Attributes => string.Compare(x.Attributes, y.Attributes, StringComparison.Ordinal),
        _ => string.Compare(x.Name, y.Name, StringComparison.OrdinalIgnoreCase),
    };
}
