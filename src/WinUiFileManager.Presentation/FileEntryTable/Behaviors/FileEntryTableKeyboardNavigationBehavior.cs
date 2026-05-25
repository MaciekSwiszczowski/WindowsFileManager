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
public sealed class FileEntryTableKeyboardNavigationBehavior : FileEntryTableBehaviorBase
{
    private void EntryTable_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        var context = Context;
        if (e.Handled
            || WinUiViewHelper.HasAnyModifier(VirtualKey.Shift,VirtualKey.Control, VirtualKey.Menu)
            || context.Table.Items.Count == 0
            || !context.Table.TryGetNavigationTargetIndex(e.Key, GetCurrentIndex(context), out var targetIndex))
        {
            return;
        }

        context.Table.SelectSingleRow(context.NavigationState, targetIndex);
        e.Handled = true;
    }

    private static int GetCurrentIndex(FileEntryTableContext context) =>
        context.NavigationState.GetCurrentIndex(context.Table)
        ?? context.Table.GetCurrentSelectedIndex();

    protected override void OnLoaded(FileEntryTableContext context)
        => context.Table.PreviewKeyDown += EntryTable_PreviewKeyDown;

    protected override void OnUnloaded(FileEntryTableContext context)
        => context.Table.PreviewKeyDown -= EntryTable_PreviewKeyDown;
}
