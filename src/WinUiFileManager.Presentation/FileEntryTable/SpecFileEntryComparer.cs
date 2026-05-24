using WinUiFileManager.Presentation.Services;

namespace WinUiFileManager.Presentation.FileEntryTable;

internal sealed class SpecFileEntryComparer : System.Collections.IComparer, IComparer<SpecFileEntryViewModel>
{
    private static readonly StringComparer TextComparer = StringComparer.CurrentCultureIgnoreCase;

    private readonly FileEntryDisplayStringCache _displayStringCache;
    private readonly SortColumn _column;
    private readonly bool _ascending;

    public SpecFileEntryComparer(SortColumn column, bool ascending, FileEntryDisplayStringCache displayStringCache)
    {
        _column = column;
        _ascending = ascending;
        _displayStringCache = displayStringCache;
    }

    public int Compare(object? x, object? y)
    {
        if (x is null || y is null)
        {
            return CompareNullable(x, y);
        }

        return x is SpecFileEntryViewModel left && y is SpecFileEntryViewModel right
            ? Compare(left, right)
            : CompareNullable(x, y);
    }

    public int Compare(SpecFileEntryViewModel? x, SpecFileEntryViewModel? y)
    {
        if (ReferenceEquals(x, y))
        {
            return 0;
        }

        if (x is not { } left)
        {
            return _ascending ? -1 : 1;
        }

        if (y is not { } right)
        {
            return _ascending ? 1 : -1;
        }

        if (SpecFileEntryViewModel.IsParentEntry(left))
        {
            return SpecFileEntryViewModel.IsParentEntry(right) ? 0 : -1;
        }

        if (SpecFileEntryViewModel.IsParentEntry(right))
        {
            return 1;
        }

        var entryKindResult = CompareEntryKind(left, right);
        if (entryKindResult != 0)
        {
            return entryKindResult;
        }

        if (left.Model?.Kind == ItemKind.Directory)
        {
            return CompareDirectoryNames(left, right);
        }

        var result = CompareByColumn(left, right);
        if (result == 0 && _column != SortColumn.Name)
        {
            result = TextComparer.Compare(left.Model?.Name, right.Model?.Name);
        }

        return _ascending ? result : -result;
    }

    private int CompareNullable(object? x, object? y)
    {
        if (ReferenceEquals(x, y))
        {
            return 0;
        }

        if (x is null)
        {
            return _ascending ? -1 : 1;
        }

        return y is null
            ? _ascending ? 1 : -1
            : 0;
    }

    private int CompareByColumn(SpecFileEntryViewModel x, SpecFileEntryViewModel y) =>
        _column switch
        {
            SortColumn.Name => TextComparer.Compare(x.Model?.Name, y.Model?.Name),
            SortColumn.Extension => TextComparer.Compare(x.Model?.Extension, y.Model?.Extension),
            SortColumn.Size => Nullable.Compare(x.Model?.Size, y.Model?.Size),
            SortColumn.Modified => Nullable.Compare(x.Model?.LastWriteTime, y.Model?.LastWriteTime),
            SortColumn.Attributes => TextComparer.Compare(GetTableAttributeText(x.Model?.Attributes), GetTableAttributeText(y.Model?.Attributes)),
            _ => TextComparer.Compare(x.Model?.Name, y.Model?.Name),
        };

    private int CompareDirectoryNames(SpecFileEntryViewModel x, SpecFileEntryViewModel y)
    {
        var result = TextComparer.Compare(x.Model?.Name, y.Model?.Name);
        return _column == SortColumn.Name && !_ascending ? -result : result;
    }

    private string? GetTableAttributeText(FileAttributes? attributes) =>
        attributes is { } value
            ? _displayStringCache.GetTableAttributes(value)
            : null;

    private static int CompareEntryKind(SpecFileEntryViewModel x, SpecFileEntryViewModel y)
    {
        var xKind = x.Model?.Kind;
        var yKind = y.Model?.Kind;
        if (xKind == yKind)
        {
            return 0;
        }

        return xKind == ItemKind.Directory ? -1 : 1;
    }
}
