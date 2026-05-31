namespace WinUiFileManager.Presentation.FileEntryTable.Behaviors;

/// <summary>
/// Owns the column sort gesture for the table. It intercepts the <see cref="TableView"/>'s built-in
/// sorting, tracks the current sort column and direction itself, paints the header sort glyph, and
/// publishes the desired order as a <see cref="FileTableSortRequestedMessage"/> so the data source
/// (rather than the view) performs the actual sort.
/// </summary>
/// <remarks>
/// Sorting is deliberately taken away from the <see cref="TableView"/> (<c>e.Handled = true</c> and
/// <c>SortDescriptions</c> is cleared) because the rows are a virtualised, externally-ordered
/// DynamicData collection — letting the control reorder its own items would fight the data source and
/// break virtualization assumptions (AGENTS.md §3). Sort state is held on the behavior and the initial
/// state is published on load. Subscribes <c>Sorting</c> in <see cref="OnLoaded"/>, detaches in
/// <see cref="OnUnloaded"/>.
/// </remarks>
public sealed class FileEntryTableSortingBehavior : FileEntryTableBehaviorBase
{
    private SortColumn _sortColumn = SortColumn.Name;
    private bool _sortAscending = true;

    protected override void OnLoaded(FileEntryTableContext context)
    {
        context.Table.Sorting += OnSorting;
        // Reflect the default sort in the header and tell the data source about it up front.
        ApplySortIndicator(context.Table);
        PublishSortRequest(context);
    }

    protected override void OnUnloaded(FileEntryTableContext context)
    {
        context.Table.Sorting -= OnSorting;
    }

    private void OnSorting(object? sender, TableViewSortingEventArgs e)
    {
        // Suppress the control's own sort; we drive ordering through the data source instead.
        e.Handled = true;
        if (FileEntryTableColumnMapping.MapColumn(e.Column.SortMemberPath) is not { } column)
        {
            return;
        }

        // Clicking the active column flips direction; clicking a new column starts ascending.

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

    /// <summary>Shows the ascending/descending glyph on the active column (and clears it on the
    /// others), then clears <c>SortDescriptions</c> so the control does not also try to sort.</summary>
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

    /// <summary>Broadcasts the requested sort column/direction to the pane's data source.</summary>
    private void PublishSortRequest(FileEntryTableContext context) =>
        context.Messenger.Send(
            new FileTableSortRequestedMessage(context.View.Identity, _sortColumn, _sortAscending));
}
