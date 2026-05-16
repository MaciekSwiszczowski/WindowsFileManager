using WinUiFileManager.Application.Messages.RequestMessages;

namespace WinUiFileManager.Presentation.FileEntryTable.Behaviors;

/// <summary>
/// Publishes table selection state and patches shifted row-range selection:
/// Shift+Up extends the range one row up, Shift+Down extends it one row down,
/// Shift+Home extends it to the first visible row, Shift+End extends it to the last visible row,
/// Shift+PageUp extends it to the first visible row when the cursor is inside the viewport
/// and not already first; otherwise it extends up by the current visible row count,
/// Shift+PageDown extends it to the last visible row when the cursor is inside the viewport
/// and not already last; otherwise it extends down by the current visible row count.
/// </summary>
public sealed class FileEntryTableKeyboardSelectionBehavior : FileEntryTableBehaviorBase
{
    private int _selectionChangeVersion;
    private bool _syncingSelection;
    private bool _shiftRangeActive;

    protected override void OnMessengerAvailable(IMessenger messenger)
    {
        messenger.Register<FileTableSelectedItemsRequestMessage>(this, OnSelectedItemsRequested);
        messenger.Register<FileTableSelectedEntriesRequestMessage>(this, OnSelectedEntriesRequested);
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
        if (_syncingSelection || !EnsureTable())
        {
            return;
        }

        var selectionChangeVersion = ++_selectionChangeVersion;
        _shiftRangeActive = false;

        if (EntryTable!.GetRowIndex(
                e.AddedItems.OfType<SpecFileEntryViewModel>().LastOrDefault()) is { } addedIndex)
        {
            NavigationState?.SetCurrent(EntryTable!, addedIndex, resetSelectionAnchor: true);
            PublishSelectionChanged();
            return;
        }

        if (EntryTable!.SelectedItems.Count == 0)
        {
            QueueEmptySelectionChanged(selectionChangeVersion);
            return;
        }

        if (GetCurrentSelectedIndex() is { } selectedIndex)
        {
            NavigationState?.SetCurrent(EntryTable!, selectedIndex, resetSelectionAnchor: true);
        }

        PublishSelectionChanged();
    }

    private bool ExtendSelection(VirtualKey key)
    {
        if (!EnsureTable() || NavigationState is null || EntryTable!.Items.Count == 0)
        {
            return false;
        }

        var currentIndex = GetCurrentIndex();
        if (currentIndex is null)
        {
            return false;
        }

        if (!_shiftRangeActive)
        {
            NavigationState.SetCurrent(EntryTable!, currentIndex.Value, resetSelectionAnchor: true);
        }

        var anchorIndex = NavigationState.GetSelectionAnchorIndex(EntryTable!) ?? currentIndex.Value;
        var cursorIndex = NavigationState.GetSelectionCursorIndex(EntryTable!) ?? currentIndex.Value;
        if (!EntryTable!.TryGetRangeTargetIndex(key, cursorIndex, out var targetIndex))
        {
            return false;
        }

        ApplySelectionRange(anchorIndex, targetIndex);
        return true;
    }

    private void ApplySelectionRange(int anchorIndex, int targetIndex)
    {
        if (EntryTable is null)
        {
            return;
        }

        anchorIndex = ClampIndex(anchorIndex) ?? 0;
        targetIndex = ClampIndex(targetIndex) ?? anchorIndex;

        var startIndex = Math.Min(anchorIndex, targetIndex);
        var endIndex = Math.Max(anchorIndex, targetIndex);

        _syncingSelection = true;
        try
        {
            EntryTable.SelectedItems.Clear();
            for (var i = startIndex; i <= endIndex; i++)
            {
                EntryTable.SelectedItems.Add(EntryTable.Items[i]);
            }
        }
        finally
        {
            _syncingSelection = false;
        }

        _shiftRangeActive = true;
        NavigationState?.SetSelectionRange(EntryTable, anchorIndex, targetIndex);
        EntryTable.ScrollRowIntoViewIfNeeded(targetIndex);
        _selectionChangeVersion++;
        PublishSelectionChanged();
    }

    private void QueueEmptySelectionChanged(int selectionChangeVersion)
    {
        if (AssociatedObject?.DispatcherQueue.TryEnqueue(
                () => PublishEmptySelectionIfCurrent(selectionChangeVersion)) != true)
        {
            PublishEmptySelectionIfCurrent(selectionChangeVersion);
        }
    }

    private void PublishEmptySelectionIfCurrent(int selectionChangeVersion)
    {
        if (selectionChangeVersion != _selectionChangeVersion
            || EntryTable?.SelectedItems.Count != 0)
        {
            return;
        }

        NavigationState?.Reset();
        PublishSelectionChanged();
    }

    private void PublishSelectionChanged()
    {
        if (AssociatedObject is null || EntryTable is null)
        {
            return;
        }

        var selectedRows = EntryTable.SelectedItems
            .OfType<SpecFileEntryViewModel>()
            .ToList();
        var selectedItems = selectedRows
            .Where(static item => !SpecFileEntryViewModel.IsParentEntry(item))
            .ToList();
        var activeItem = NavigationState?.GetSelectionCursorIndex(EntryTable) is { } cursorIndex
            ? EntryTable.Items[cursorIndex] as SpecFileEntryViewModel
            : NavigationState?.GetCurrentItem(EntryTable) ?? EntryTable.SelectedItem as SpecFileEntryViewModel;

        var messenger = GetRequiredMessenger();

        messenger.Send(
            new FileTableSelectionChangedMessage(
                AssociatedObject.Identity,
                selectedItems,
                selectedRows.Any(static item => SpecFileEntryViewModel.IsParentEntry(item)),
                activeItem));
    }

    private int? GetCurrentIndex() =>
        (EntryTable is not null ? NavigationState?.GetSelectionCursorIndex(EntryTable) : null)
        ?? (EntryTable is not null ? NavigationState?.GetCurrentIndex(EntryTable) : null)
        ?? GetCurrentSelectedIndex()
        ?? (EntryTable?.Items.Count > 0 ? 0 : null);

    private int? GetCurrentSelectedIndex()
    {
        if (EntryTable is null)
        {
            return null;
        }

        if (EntryTable.SelectedIndex >= 0)
        {
            return EntryTable.SelectedIndex;
        }

        if (EntryTable.GetRowIndex(
                EntryTable.SelectedItem as SpecFileEntryViewModel) is { } selectedItemIndex)
        {
            return selectedItemIndex;
        }

        foreach (var item in EntryTable.SelectedItems.OfType<SpecFileEntryViewModel>().Reverse())
        {
            if (EntryTable.GetRowIndex(item) is { } selectedIndex)
            {
                return selectedIndex;
            }
        }

        return null;
    }

    private int? ClampIndex(int? index) => EntryTable?.ClampIndex(index);

    private void OnSelectedItemsRequested(object recipient, FileTableSelectedItemsRequestMessage message)
    {
        if (AssociatedObject is null || message.Identity != AssociatedObject.Identity)
        {
            return;
        }

        message.Reply(GetSelectedItemsSnapshot());
    }

    private void OnSelectedEntriesRequested(object recipient, FileTableSelectedEntriesRequestMessage message)
    {
        if (AssociatedObject is null || message.Identity != AssociatedObject.Identity)
        {
            return;
        }

        message.Reply(GetSelectedItemsSnapshot()
            .Select(static item => item.Model)
            .OfType<FileSystemEntryModel>()
            .ToList());
    }

    private IReadOnlyList<SpecFileEntryViewModel> GetSelectedItemsSnapshot()
    {
        if (EntryTable is null)
        {
            return [];
        }

        return EntryTable.SelectedItems
            .OfType<SpecFileEntryViewModel>()
            .Where(static item => !SpecFileEntryViewModel.IsParentEntry(item))
            .ToList();
    }

    protected override void OnTableAttached(TableView table)
    {
        table.PreviewKeyDown += EntryTable_PreviewKeyDown;
        table.SelectionChanged += EntryTable_SelectionChanged;
    }

    protected override void OnTableDetaching(TableView table)
    {
        table.PreviewKeyDown -= EntryTable_PreviewKeyDown;
        table.SelectionChanged -= EntryTable_SelectionChanged;
    }
}
