namespace WinUiFileManager.Presentation.FileEntryTable.Behaviors;

public sealed class FileEntryTableSortingBehavior : FileEntryTableBehaviorBase
{
    private FileEntryColumn _sortColumn = FileEntryColumn.Name;
    private bool _sortAscending = true;

    protected override void OnLoaded(FileEntryTableBehaviorContext context)
    {
        context.Table.Sorting += OnSorting;
        ApplySort(context.Table);
    }

    protected override void OnUnloaded(FileEntryTableBehaviorContext context)
    {
        context.Table.Sorting -= OnSorting;
    }

    private void OnSorting(object? sender, TableViewSortingEventArgs e)
    {
        e.Handled = true;
        if (FileEntryTableColumnMapping.MapColumn(e.Column.SortMemberPath) is not { } column)
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

        ApplySort(Context.Table);
    }

    private void ApplySort(TableView table)
    {
        var direction = _sortAscending ? SortDirection.Ascending : SortDirection.Descending;
        foreach (var column in table.Columns)
        {
            column.SortDirection = FileEntryTableColumnMapping.MapColumn(column.SortMemberPath) == _sortColumn
                ? direction
                : null;
        }

        table.SortDescriptions.Clear();
        table.SortDescriptions.Add(new WinUI.TableView.SortDescription(
            FileEntryTableColumnMapping.MapSortMemberPath(_sortColumn),
            SortDirection.Ascending,
            new SpecFileEntryComparer(_sortColumn, _sortAscending),
            static item => item));
    }
}
