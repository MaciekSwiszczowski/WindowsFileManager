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
            result = TextComparer.Compare(left.Name, right.Name);
        }

        return _ascending ? result : -result;
    }

    private int CompareByColumn(SpecFileEntryViewModel x, SpecFileEntryViewModel y) =>
        _column switch
        {
            FileEntryColumn.Name => TextComparer.Compare(x.Name, y.Name),
            FileEntryColumn.Extension => TextComparer.Compare(x.Extension, y.Extension),
            FileEntryColumn.Size => Nullable.Compare(x.Model?.Size, y.Model?.Size),
            FileEntryColumn.Modified => x.Modified.CompareTo(y.Modified),
            FileEntryColumn.Attributes => TextComparer.Compare(x.Attributes, y.Attributes),
            _ => TextComparer.Compare(x.Name, y.Name),
        };

    private static int CompareEntryKind(SpecFileEntryViewModel x, SpecFileEntryViewModel y)
    {
        if (x.EntryKind == y.EntryKind)
        {
            return 0;
        }

        return x.EntryKind == FileEntryKind.Folder ? -1 : 1;
    }
}
