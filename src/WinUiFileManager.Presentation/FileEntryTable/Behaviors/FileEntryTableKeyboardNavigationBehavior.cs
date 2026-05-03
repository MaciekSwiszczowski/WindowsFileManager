namespace WinUiFileManager.Presentation.FileEntryTable.Behaviors;

/// <summary>
/// Handles plain table keyboard navigation with the shared table navigation state:
/// Up selects the previous row,
/// Down selects the next row,
/// Home selects the first visible row,
/// End selects the last visible row,
/// PageUp selects the first visible row when the current row is inside the viewport
/// and not already first; otherwise it moves up by the current visible row count,
/// PageDown selects the last visible row when the current row is inside the viewport
/// and not already last; otherwise it moves down by the current visible row count.
/// Page movement clamps at the list boundaries and scrolls the target row into view.
/// </summary>
public sealed class FileEntryTableKeyboardNavigationBehavior : FileEntryTableBehavior
{
    protected override void OnAttached()
    {
        base.OnAttached();
        TrackTableOnLoaded();
    }

    protected override void OnDetaching()
    {
        base.OnDetaching();
    }

    private void EntryTable_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Handled
            || FileEntryTableBehaviorHelper.HasAnyModifier(
                VirtualKey.Shift,
                VirtualKey.Control,
                VirtualKey.Menu)
            || !EnsureTable()
            || NavigationState is null
            || EntryTable!.Items.Count == 0
            || !FileEntryTableBehaviorHelper.TryGetNavigationTargetIndex(
                EntryTable,
                e.Key,
                GetCurrentIndex(),
                out var targetIndex))
        {
            return;
        }

        FileEntryTableBehaviorHelper.SelectSingleRow(EntryTable, NavigationState, targetIndex);
        e.Handled = true;
    }

    private int GetCurrentIndex()
    {
        if (EntryTable is null)
        {
            return 0;
        }

        return NavigationState?.GetCurrentIndex(EntryTable)
            ?? FileEntryTableBehaviorHelper.GetCurrentSelectedIndex(EntryTable);
    }

    protected override void OnTableAttached(TableView table)
    {
        table.PreviewKeyDown += EntryTable_PreviewKeyDown;
    }

    protected override void OnTableDetaching(TableView table)
    {
        table.PreviewKeyDown -= EntryTable_PreviewKeyDown;
    }
}
