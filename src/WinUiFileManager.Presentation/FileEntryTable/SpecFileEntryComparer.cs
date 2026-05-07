namespace WinUiFileManager.Presentation.FileEntryTable;

internal sealed class SpecFileEntryComparer : System.Collections.IComparer
{
    private static readonly StringComparer TextComparer = StringComparer.CurrentCultureIgnoreCase;

    private readonly FileEntryColumn _column;
    private readonly bool _ascending;

    public SpecFileEntryComparer(FileEntryColumn column, bool ascending)
    {
        _column = column;
        _ascending = ascending;
    }

    public int Compare(object? x, object? y)
    {
        if (ReferenceEquals(x, y))
        {
            return 0;
        }

        if (x is not SpecFileEntryViewModel left)
        {
            return _ascending ? -1 : 1;
        }

        if (y is not SpecFileEntryViewModel right)
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

        var result = CompareByColumn(left, right);
        if (result == 0 && _column != FileEntryColumn.Name)
        {
            result = TextComparer.Compare(left.Model?.Name, right.Model?.Name);
        }

        return _ascending ? result : -result;
    }

    private int CompareByColumn(SpecFileEntryViewModel x, SpecFileEntryViewModel y) =>
        _column switch
        {
            FileEntryColumn.Name => TextComparer.Compare(x.Model?.Name, y.Model?.Name),
            FileEntryColumn.Extension => TextComparer.Compare(x.Model?.Extension, y.Model?.Extension),
            FileEntryColumn.Size => Nullable.Compare(x.Model?.Size, y.Model?.Size),
            FileEntryColumn.Modified => Nullable.Compare(x.Model?.LastWriteTimeUtc, y.Model?.LastWriteTimeUtc),
            FileEntryColumn.Attributes => TextComparer.Compare(x.Model?.Attributes.ToString(), y.Model?.Attributes.ToString()),
            _ => TextComparer.Compare(x.Model?.Name, y.Model?.Name),
        };

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
