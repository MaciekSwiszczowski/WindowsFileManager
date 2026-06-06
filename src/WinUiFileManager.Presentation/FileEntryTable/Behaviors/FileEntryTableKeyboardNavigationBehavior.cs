namespace WinUiFileManager.Presentation.FileEntryTable.Behaviors;

/// <summary>
/// Handles non-arrow table keyboard navigation that WinUI.TableView does not provide consistently.
/// </summary>
/// <remarks>
/// Plain <c>Up</c>/<c>Down</c> are intentionally left to native <see cref="TableView"/> keyboard handling.
/// Replacing those keys here fights the control's internal repeat/cursor logic and can make held arrow-key
/// navigation visually skip rows. This behavior only handles <c>Home</c>, <c>End</c>, <c>PageUp</c>, and
/// <c>PageDown</c>, then synchronizes the native keyboard anchor so the next arrow key starts from the row
/// the user sees selected.
/// </remarks>
public sealed class FileEntryTableKeyboardNavigationBehavior : FileEntryTableBehaviorBase
{
    protected override void OnLoaded(FileEntryTableContext context)
        => context.Table.PreviewKeyDown += EntryTable_PreviewKeyDown;

    // Matching -= keeps the PreviewKeyDown subscription balanced when the behavior detaches.
    protected override void OnUnloaded(FileEntryTableContext context)
        => context.Table.PreviewKeyDown -= EntryTable_PreviewKeyDown;

    private void EntryTable_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        var context = Context;
        if (e.Handled
            || WinUiViewHelper.HasAnyModifier(VirtualKey.Shift, VirtualKey.Control, VirtualKey.Menu)
            || context.Table.Items.Count == 0
            || !context.Table.TryGetJumpNavigationTargetIndex(e.Key, GetCurrentIndex(context), out var targetIndex))
        {
            return;
        }

        SelectSingleRow(context, targetIndex);
        e.Handled = true;
    }

    private static int GetCurrentIndex(FileEntryTableContext context)
    {
        if (context.NavigationState.GetCurrentIndex(context.Table) is { } currentIndex)
        {
            return currentIndex;
        }

        if (context.Table.SelectedIndex >= 0)
        {
            return context.Table.SelectedIndex;
        }

        if (context.Table.GetRowIndex(context.Table.SelectedItem as FileListingRow) is { } selectedItemIndex)
        {
            return selectedItemIndex;
        }

        foreach (var item in context.Table.SelectedItems.OfType<FileListingRow>().Reverse())
        {
            if (context.Table.GetRowIndex(item) is { } selectedIndex)
            {
                return selectedIndex;
            }
        }

        return 0;
    }

    private static void SelectSingleRow(FileEntryTableContext context, int targetIndex)
    {
        if (context.Table.Items[targetIndex] is not { } item)
        {
            return;
        }

        context.NavigationState.SetCurrent(context.Table, targetIndex, resetSelectionAnchor: true);
        TableViewKeyboardAnchorSynchronizer.Sync(context.Table, targetIndex);

        if (context.Table.SelectedItems.Count != 1 || !ReferenceEquals(context.Table.SelectedItems[0], item))
        {
            context.Table.SelectedItems.Clear();
            context.Table.SelectedItems.Add(item);
        }

        context.Table.ScrollRowIntoViewIfNeeded(targetIndex);
    }
}
