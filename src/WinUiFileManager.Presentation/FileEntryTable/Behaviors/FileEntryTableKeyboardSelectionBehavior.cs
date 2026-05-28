using WinUiFileManager.Application.Messages.RequestMessages;
using WinUiFileManager.Presentation.Threading;

namespace WinUiFileManager.Presentation.FileEntryTable.Behaviors;

/// <summary>
/// Publishes table selection state and patches shifted row-range selection:
/// Shift+Up extends the range one row up, Shift+Down extends it one row down,
/// Shift+Home extends it to the first visible row, Shift+End extends it to the last visible row,
/// Shift+PageUp extends it to the first visible row when the cursor is inside the viewport and not already first; otherwise it extends up by the current visible row count,
/// Shift+PageDown extends it to the last visible row when the cursor is inside the viewport and not already last; otherwise it extends down by the current visible row count.
/// </summary>
public sealed class FileEntryTableKeyboardSelectionBehavior : FileEntryTableBehaviorBase
{
    private bool _syncingSelection;
    private bool _shiftRangeActive;

    protected override void OnLoaded(FileEntryTableContext context)
    {
        context.Messenger.Register(
            this,
            IdentityFilter.For<FileTableSelectedItemsRequestMessage>(context.View.Identity, OnSelectedItemsRequested));
        context.Messenger.Register(
            this,
            IdentityFilter.For<FileTableSelectedEntriesRequestMessage>(context.View.Identity, OnSelectedEntriesRequested));
        context.Table.PreviewKeyDown += EntryTable_PreviewKeyDown;
        context.Table.SelectionChanged += EntryTable_SelectionChanged;
    }

    protected override void OnUnloaded(FileEntryTableContext context)
    {
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
                e.AddedItems.OfType<SpecFileEntryViewModel>().LastOrDefault()) is { } addedIndex)
        {
            context.NavigationState.SetCurrent(context.Table, addedIndex, resetSelectionAnchor: true);
            PublishSelectionChanged();
            return;
        }

        if (GetCurrentSelectedIndex(context) is { } selectedIndex)
        {
            context.NavigationState.SetCurrent(context.Table, selectedIndex, resetSelectionAnchor: true);
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
            .OfType<SpecFileEntryViewModel>()
            .ToList();
        var selectedItems = selectedRows
            .Where(static item => !SpecFileEntryViewModel.IsParentEntry(item))
            .ToList();

        return new FileTableSelectionChangedMessage(
            context.View.Identity,
            selectedItems,
            selectedRows.Any(static item => SpecFileEntryViewModel.IsParentEntry(item)),
            GetActiveItem(context));
    }

    private static SpecFileEntryViewModel? GetActiveItem(FileEntryTableContext context) =>
        context.NavigationState.GetSelectionCursorIndex(context.Table) is { } cursorIndex
            ? context.Table.Items[cursorIndex] as SpecFileEntryViewModel
            : context.NavigationState.GetCurrentItem(context.Table) ?? context.Table.SelectedItem as SpecFileEntryViewModel;

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
                context.Table.SelectedItem as SpecFileEntryViewModel) is { } selectedItemIndex)
        {
            return selectedItemIndex;
        }

        foreach (var item in context.Table.SelectedItems.OfType<SpecFileEntryViewModel>().Reverse())
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

    private Task<IReadOnlyList<SpecFileEntryViewModel>> GetSelectedItemsSnapshotAsync()
    {
        var context = Context;
        return context.View.DispatcherQueue.HasThreadAccess ?
            Task.FromResult(CreateSelectedItemsSnapshot(context)) :
            context.View.DispatcherQueue.RunAsync(() => CreateSelectedItemsSnapshot(context));
    }

    private static IReadOnlyList<SpecFileEntryViewModel> CreateSelectedItemsSnapshot(FileEntryTableContext context)
    {
        return context.Table.SelectedItems
            .OfType<SpecFileEntryViewModel>()
            .Where(static item => !SpecFileEntryViewModel.IsParentEntry(item))
            .ToList();
    }
}
