using WinUiFileManager.Presentation.Services;

namespace WinUiFileManager.Presentation.FileEntryTable;

/// <summary>
/// Orders <see cref="SpecFileEntryViewModel"/> rows for the file table according to the active
/// <see cref="SortColumn"/> and direction, applying the file-manager ordering rules: the ".." parent
/// row always sorts first, directories always group before files, directories sort by name regardless
/// of the chosen column, and a stable name-based tiebreak is used when the primary key compares equal.
/// </summary>
/// <remarks>
/// Implements both the non-generic <see cref="System.Collections.IComparer"/> (required by the
/// <see cref="TableView"/> integration) and the typed <see cref="IComparer{T}"/>. Attribute
/// comparison reuses the process-wide <see cref="FileEntryDisplayStringCache"/> so it sorts by the same
/// text the UI displays without re-deriving it per comparison.
/// </remarks>
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

        // The ".." row is pinned to the top regardless of column/direction.
        if (SpecFileEntryViewModel.IsParentEntry(left))
        {
            return SpecFileEntryViewModel.IsParentEntry(right) ? 0 : -1;
        }

        if (SpecFileEntryViewModel.IsParentEntry(right))
        {
            return 1;
        }

        // Directories always group ahead of files irrespective of the active sort column.
        var entryKindResult = CompareEntryKind(left, right);
        if (entryKindResult != 0)
        {
            return entryKindResult;
        }

        // Directories are always ordered by name (only the name column's direction applies to them).
        if (left.Model?.Kind == ItemKind.Directory)
        {
            return CompareDirectoryNames(left, right);
        }

        var result = CompareByColumn(left, right);
        // Stable tiebreak: equal non-name keys fall back to name so ordering is deterministic.
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
