using WinUiFileManager.Presentation.FileEntryTable;

namespace WinUiFileManager.Presentation.FileEntryTable.Behaviors;

public sealed class FileEntryTableSortingBehavior : FileEntryTableBehavior
{
    private FileEntryColumn _sortColumn = FileEntryColumn.Name;
    private bool _sortAscending = true;

    protected override void OnAttached()
    {
        base.OnAttached();
        TrackTableOnLoaded();
    }

    protected override void OnTableAttached(TableView table)
    {
        table.Sorting += OnSorting;
        ApplySort();
    }

    protected override void OnTableDetaching(TableView table)
    {
        table.Sorting -= OnSorting;
    }

    private void OnSorting(object? sender, TableViewSortingEventArgs e)
    {
        if (EntryTable is null)
        {
            return;
        }

        e.Handled = true;
        if (FileEntryTableBehaviorHelper.MapColumn(e.Column.SortMemberPath) is not { } column)
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
        if (EntryTable is null)
        {
            return;
        }

        var direction = _sortAscending ? SortDirection.Ascending : SortDirection.Descending;
        foreach (var column in EntryTable.Columns)
        {
            column.SortDirection = FileEntryTableBehaviorHelper.MapColumn(column.SortMemberPath) == _sortColumn
                ? direction
                : null;
        }

        EntryTable.SortDescriptions.Clear();
        EntryTable.SortDescriptions.Add(new WinUI.TableView.SortDescription(
            FileEntryTableBehaviorHelper.MapSortMemberPath(_sortColumn),
            SortDirection.Ascending,
            new SpecFileEntryComparer(_sortColumn, _sortAscending),
            static item => item));
    }
}
