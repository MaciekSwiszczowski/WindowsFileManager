namespace WinUiFileManager.Presentation.FileEntryTable.Behaviors;

public sealed class FileEntryTableSortingBehavior : FileEntryTableBehaviorBase
{
    private SortColumn _sortColumn = SortColumn.Name;
    private bool _sortAscending = true;

    protected override void OnLoaded(FileEntryTableContext context)
    {
        context.Table.Sorting += OnSorting;
        ApplySortIndicator(context.Table);
        PublishSortRequest(context);
    }

    protected override void OnUnloaded(FileEntryTableContext context)
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

        ApplySortIndicator(Context.Table);
        PublishSortRequest(Context);
    }

    private void ApplySortIndicator(TableView table)
    {
        var direction = _sortAscending ? SortDirection.Ascending : SortDirection.Descending;
        foreach (var column in table.Columns)
        {
            column.SortDirection = FileEntryTableColumnMapping.MapColumn(column.SortMemberPath) == _sortColumn
                ? direction
                : null;
        }

        table.SortDescriptions.Clear();
    }

    private void PublishSortRequest(FileEntryTableContext context) =>
        context.Messenger.Send(
            new FileTableSortRequestedMessage(context.View.Identity, _sortColumn, _sortAscending));
}
