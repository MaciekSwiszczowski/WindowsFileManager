using WinUiFileManager.Presentation.FileEntryTable;

namespace WinUiFileManager.Presentation.FileEntryTable.Behaviors;

public sealed class FileEntryTableSortingBehavior : Behavior<SpecFileEntryTableView>
{
    private FileEntryColumn _sortColumn = FileEntryColumn.Name;
    private bool _sortAscending = true;
    private TableView? _entryTable;

    protected override void OnAttached()
    {
        base.OnAttached();
        AssociatedObject.Loaded += OnLoaded;
    }

    protected override void OnDetaching()
    {
        if (AssociatedObject is { } view)
        {
            view.Loaded -= OnLoaded;
        }

        if (_entryTable is not null)
        {
            _entryTable.Sorting -= OnSorting;
        }

        _entryTable = null;

        base.OnDetaching();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (AssociatedObject is null)
        {
            return;
        }

        var table = AssociatedObject.Table;

        if (ReferenceEquals(_entryTable, table))
        {
            ApplySort();
            return;
        }

        if (_entryTable is not null)
        {
            _entryTable.Sorting -= OnSorting;
        }

        _entryTable = table;

        if (_entryTable is not null)
        {
            _entryTable.Sorting += OnSorting;
            ApplySort();
        }
    }

    private void OnSorting(object? sender, TableViewSortingEventArgs e)
    {
        if (_entryTable is null)
        {
            return;
        }

        e.Handled = true;
        if (MapColumn(e.Column.SortMemberPath) is not { } column)
        {
            return;
        }

        if (_sortColumn == column)
        {
            _sortAscending = !_sortAscending;
        }
        else
        {
            _sortColumn = column;
            _sortAscending = true;
        }

        ApplySort();
    }

    private void ApplySort()
    {
        if (_entryTable is null)
        {
            return;
        }

        var direction = _sortAscending ? SortDirection.Ascending : SortDirection.Descending;
        foreach (var column in _entryTable.Columns)
        {
            column.SortDirection = MapColumn(column.SortMemberPath) == _sortColumn ? direction : null;
        }

        _entryTable.SortDescriptions.Clear();
        _entryTable.SortDescriptions.Add(new WinUI.TableView.SortDescription(
            MapSortMemberPath(_sortColumn),
            SortDirection.Ascending,
            new SpecFileEntryComparer(_sortColumn, _sortAscending),
            static item => item));
    }

    private static FileEntryColumn? MapColumn(string? sortMemberPath) =>
        sortMemberPath switch
        {
            nameof(SpecFileEntryViewModel.Name) => FileEntryColumn.Name,
            nameof(SpecFileEntryViewModel.Extension) => FileEntryColumn.Extension,
            nameof(SpecFileEntryViewModel.Size) => FileEntryColumn.Size,
            nameof(SpecFileEntryViewModel.Modified) => FileEntryColumn.Modified,
            nameof(SpecFileEntryViewModel.Attributes) => FileEntryColumn.Attributes,
            _ => null,
        };

    private static string MapSortMemberPath(FileEntryColumn column) =>
        column switch
        {
            FileEntryColumn.Name => nameof(SpecFileEntryViewModel.Name),
            FileEntryColumn.Extension => nameof(SpecFileEntryViewModel.Extension),
            FileEntryColumn.Size => nameof(SpecFileEntryViewModel.Size),
            FileEntryColumn.Modified => nameof(SpecFileEntryViewModel.Modified),
            FileEntryColumn.Attributes => nameof(SpecFileEntryViewModel.Attributes),
            _ => nameof(SpecFileEntryViewModel.Name),
        };
}
