using Microsoft.Xaml.Interactivity;

namespace WinUiFileManager.Presentation.FileEntryTable;

public sealed class FileEntryTableSortingBehavior : Behavior<SpecFileEntryTableView>
{
    private const string EntryTableName = "EntryTable";

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

        var table = AssociatedObject.FindDescendant<TableView>(static candidate => candidate.Name == EntryTableName);
        if (ReferenceEquals(_entryTable, table))
        {
            SyncSortIndicators();
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
            SyncSortIndicators();
        }
    }

    private void OnSorting(object? sender, TableViewSortingEventArgs e)
    {
        e.Handled = true;
        if (MapColumn(e.Column.SortMemberPath) is not { } column || AssociatedObject is null)
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

        SyncSortIndicators();
        AssociatedObject.SetItemComparer(SpecFileEntryComparer.Create(_sortColumn, _sortAscending));
    }

    private void SyncSortIndicators()
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
}
