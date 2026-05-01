namespace WinUiFileManager.Presentation.FileEntryTable;

internal sealed class SpecFileEntryComparer : IComparer<SpecFileEntryViewModel>
{
    private static readonly StringComparer TextComparer = StringComparer.CurrentCultureIgnoreCase;

    private readonly FileEntryColumn _column;
    private readonly bool _ascending;

    private SpecFileEntryComparer(FileEntryColumn column, bool ascending)
    {
        _column = column;
        _ascending = ascending;
    }

    public static SpecFileEntryComparer Create(FileEntryColumn column, bool ascending) =>
        new(column, ascending);

    public int Compare(SpecFileEntryViewModel? x, SpecFileEntryViewModel? y)
    {
        if (ReferenceEquals(x, y))
        {
            return 0;
        }

        if (x is null)
        {
            return _ascending ? -1 : 1;
        }

        if (y is null)
        {
            return _ascending ? 1 : -1;
        }

        var result = CompareByColumn(x, y);
        if (result == 0 && _column != FileEntryColumn.Name)
        {
            result = TextComparer.Compare(x.Name, y.Name);
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
}
