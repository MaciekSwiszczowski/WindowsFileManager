using WinUiFileManager.Application.Messages.RequestMessages;
using WinUiFileManager.Presentation.Threading;

namespace WinUiFileManager.Presentation.FileEntryTable.Behaviors;

/// <summary>
/// Publishes table selection state, keeps native keyboard anchors synchronized, and patches shifted row-range selection:
/// Shift+Up extends the range one row up, Shift+Down extends it one row down,
/// Shift+Home extends it to the first visible row, Shift+End extends it to the last visible row,
/// Shift+PageUp extends it to the first visible row when the cursor is inside the viewport and not already first; otherwise it extends up by the current visible row count,
/// Shift+PageDown extends it to the last visible row when the cursor is inside the viewport and not already last; otherwise it extends down by the current visible row count.
/// </summary>
/// <remarks>
/// Owns table selection coordination: it publishes selection changes, answers selection snapshot requests,
/// keeps WinUI.TableView's native keyboard anchor aligned with the selected row, and implements the
/// anchor/cursor based Shift-range selection that the stock <see cref="TableView"/> does not provide.
/// <para>
/// Pane-scoped: request messages are registered through the messenger wrapper keyed on the view identity
/// (AGENTS.md §4), so a request aimed at the other pane is ignored.
/// </para>
/// <para>
/// <see cref="_syncingSelection"/> guards against re-entrancy: when we mutate
/// <c>SelectedItems</c> ourselves the resulting <c>SelectionChanged</c> must not be treated as a
/// user-driven change. <see cref="_shiftRangeActive"/> remembers that a Shift range is in progress so
/// the anchor is preserved across repeated Shift presses.
/// </para>
/// <para>
/// Event discipline (AGENTS.md §5): <c>PreviewKeyDown</c> and <c>SelectionChanged</c> are subscribed in
/// <see cref="OnLoaded"/> and detached in <see cref="OnUnloaded"/>; messenger unregistration is done by
/// the base class.
/// </para>
/// </remarks>
public sealed class FileEntryTableKeyboardSelectionBehavior : FileEntryTableBehaviorBase
{
    // True while we are programmatically rewriting SelectedItems, so the SelectionChanged handler
    // ignores the echo and does not clobber the anchor/cursor we just set.
    private bool _syncingSelection;
    // True once a Shift range has begun, so subsequent Shift presses extend from the original anchor
    // instead of resetting it.
    private bool _shiftRangeActive;

    protected override void OnLoaded(FileEntryTableContext context)
    {
        context.Messenger.Register<FileTableSelectedItemsRequestMessage>(this, context.View.Identity, OnSelectedItemsRequested);
        context.Messenger.Register<FileTableSelectedEntriesRequestMessage>(this, context.View.Identity, OnSelectedEntriesRequested);
        context.Table.PreviewKeyDown += EntryTable_PreviewKeyDown;
        context.Table.SelectionChanged += EntryTable_SelectionChanged;
    }

    protected override void OnUnloaded(FileEntryTableContext context)
    {
        // Reverse both UI event subscriptions; the base class drops the messenger registrations.
        context.Table.PreviewKeyDown -= EntryTable_PreviewKeyDown;
        context.Table.SelectionChanged -= EntryTable_SelectionChanged;
    }

    private void EntryTable_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Handled
            || !WinUiViewHelper.IsModifierDown(VirtualKey.Shift)
            || WinUiViewHelper.IsModifierDown(VirtualKey.Control))
        {
            return;
        }

        if (ExtendSelection(e.Key))
        {
            e.Handled = true;
        }
    }

    private void EntryTable_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingSelection)
        {
            return;
        }

        var context = Context;
        _shiftRangeActive = false;

        if (context.Table.GetRowIndex(
                e.AddedItems.OfType<FileListingRow>().LastOrDefault()) is { } addedIndex)
        {
            context.NavigationState.SetCurrent(context.Table, addedIndex, resetSelectionAnchor: true);
            TableViewKeyboardAnchorSynchronizer.Sync(context.Table, addedIndex);
            PublishSelectionChanged();
            return;
        }

        if (GetCurrentSelectedIndex(context) is { } selectedIndex)
        {
            context.NavigationState.SetCurrent(context.Table, selectedIndex, resetSelectionAnchor: true);
            TableViewKeyboardAnchorSynchronizer.Sync(context.Table, selectedIndex);
        }

        PublishSelectionChanged();
    }

    private bool ExtendSelection(VirtualKey key)
    {
        var context = Context;
        if (context.Table.Items.Count == 0)
        {
            return false;
        }

        var currentIndex = GetCurrentIndex(context);
        if (currentIndex is null)
        {
            return false;
        }

        if (!_shiftRangeActive)
        {
            context.NavigationState.SetCurrent(context.Table, currentIndex.Value, resetSelectionAnchor: true);
        }

        var anchorIndex = context.NavigationState.GetSelectionAnchorIndex(context.Table) ?? currentIndex.Value;
        var cursorIndex = context.NavigationState.GetSelectionCursorIndex(context.Table) ?? currentIndex.Value;
        if (!context.Table.TryGetRangeTargetIndex(key, cursorIndex, out var targetIndex))
        {
            return false;
        }

        ApplySelectionRange(context, anchorIndex, targetIndex);
        return true;
    }

    private void ApplySelectionRange(FileEntryTableContext context, int anchorIndex, int targetIndex)
    {
        anchorIndex = ClampIndex(anchorIndex) ?? 0;
        targetIndex = ClampIndex(targetIndex) ?? anchorIndex;

        var startIndex = Math.Min(anchorIndex, targetIndex);
        var endIndex = Math.Max(anchorIndex, targetIndex);

        _syncingSelection = true;
        try
        {
            context.Table.SelectedItems.Clear();
            for (var i = startIndex; i <= endIndex; i++)
            {
                context.Table.SelectedItems.Add(context.Table.Items[i]);
            }
        }
        finally
        {
            _syncingSelection = false;
        }

        _shiftRangeActive = true;
        context.NavigationState.SetSelectionRange(context.Table, anchorIndex, targetIndex);
        TableViewKeyboardAnchorSynchronizer.Sync(context.Table, targetIndex);
        context.Table.ScrollRowIntoViewIfNeeded(targetIndex);
        PublishSelectionChanged();
    }

    private void PublishSelectionChanged()
    {
        var context = Context;
        context.Messenger.SendFileTableSelectionChanged(CreateSelectionChangedMessage(context), context.View.DispatcherQueue);
    }

    private FileTableSelectionChangedMessage CreateSelectionChangedMessage(FileEntryTableContext context)
    {
        var selectedRows = context.Table.SelectedItems
            .OfType<FileListingRow>()
            .ToList();
        var selectedItems = selectedRows
            .Where(static item => !FileListingRow.IsParentEntry(item))
            .ToList();

        return new FileTableSelectionChangedMessage(
            context.View.Identity,
            selectedItems,
            selectedRows.Any(static item => FileListingRow.IsParentEntry(item)),
            GetActiveItem(context));
    }

    private static FileListingRow? GetActiveItem(FileEntryTableContext context) =>
        context.NavigationState.GetSelectionCursorIndex(context.Table) is { } cursorIndex
            ? context.Table.Items[cursorIndex] as FileListingRow
            : context.NavigationState.GetCurrentItem(context.Table) ?? context.Table.SelectedItem as FileListingRow;

    private int? GetCurrentIndex(FileEntryTableContext context) =>
        context.NavigationState.GetSelectionCursorIndex(context.Table)
        ?? context.NavigationState.GetCurrentIndex(context.Table)
        ?? GetCurrentSelectedIndex(context)
        ?? (context.Table.Items.Count > 0 ? 0 : null);

    private static int? GetCurrentSelectedIndex(FileEntryTableContext context)
    {
        if (context.Table.SelectedIndex >= 0)
        {
            return context.Table.SelectedIndex;
        }

        if (context.Table.GetRowIndex(
                context.Table.SelectedItem as FileListingRow) is { } selectedItemIndex)
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

        return null;
    }

    private int? ClampIndex(int? index) => Context.Table.ClampIndex(index);

    private void OnSelectedItemsRequested(FileTableSelectedItemsRequestMessage message)
    {
        message.Reply(GetSelectedItemsSnapshotAsync());
    }

    private void OnSelectedEntriesRequested(FileTableSelectedEntriesRequestMessage message)
    {
        message.Reply(CreateSelectedItemsSnapshot(Context)
            .Select(static item => item.Model)
            .OfType<FileSystemEntryModel>()
            .ToList());
    }

    /// <summary>Snapshots the selected rows, marshalling onto the UI thread first because
    /// <c>SelectedItems</c> must only be read on the dispatcher (AGENTS.md §6). Returns synchronously
    /// when already on the UI thread.</summary>
    private Task<IReadOnlyList<FileListingRow>> GetSelectedItemsSnapshotAsync()
    {
        var context = Context;
        return context.View.DispatcherQueue.HasThreadAccess ?
            Task.FromResult(CreateSelectedItemsSnapshot(context)) :
            context.View.DispatcherQueue.RunAsync(() => CreateSelectedItemsSnapshot(context));
    }

    private static IReadOnlyList<FileListingRow> CreateSelectedItemsSnapshot(FileEntryTableContext context)
    {
        return context.Table.SelectedItems
            .OfType<FileListingRow>()
            .Where(static item => !FileListingRow.IsParentEntry(item))
            .ToList();
    }
}
