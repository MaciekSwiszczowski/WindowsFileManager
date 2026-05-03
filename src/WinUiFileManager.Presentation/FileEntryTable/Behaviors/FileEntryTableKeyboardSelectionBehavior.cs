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
public sealed class FileEntryTableKeyboardSelectionBehavior : FileEntryTableBehavior
{
    private TableView? _entryTable;
    private bool _selectionEventsAttached;
    private bool _syncingSelection;
    private bool _shiftRangeActive;

    protected override void OnAttached()
    {
        base.OnAttached();
        AssociatedObject.Loaded += OnLoaded;
        WeakReferenceMessenger.Default.Register<FileTableSelectedItemsRequestMessage>(this, OnSelectedItemsRequested);
    }

    protected override void OnDetaching()
    {
        if (AssociatedObject is { } view)
        {
            view.Loaded -= OnLoaded;
        }

        DetachTableEvents();
        _entryTable = null;
        WeakReferenceMessenger.Default.Unregister<FileTableSelectedItemsRequestMessage>(this);

        base.OnDetaching();
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => EnsureTable();

    private void EntryTable_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Handled
            || !FileEntryTableBehaviorHelper.IsModifierDown(VirtualKey.Shift)
            || FileEntryTableBehaviorHelper.IsModifierDown(VirtualKey.Control))
        {
            return;
        }

        if (!ExtendSelection(e.Key))
        {
            return;
        }

        e.Handled = true;
    }

    private void EntryTable_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingSelection || !EnsureTable())
        {
            return;
        }

        _shiftRangeActive = false;

        if (FileEntryTableBehaviorHelper.GetRowIndex(
                _entryTable!,
                e.AddedItems.OfType<SpecFileEntryViewModel>().LastOrDefault()) is { } addedIndex)
        {
            NavigationState?.SetCurrent(_entryTable!, addedIndex, resetSelectionAnchor: true);
            PublishSelectionChanged();
            return;
        }

        if (_entryTable!.SelectedItems.Count == 0)
        {
            NavigationState?.Reset();
            PublishSelectionChanged();
            return;
        }

        if (GetCurrentSelectedIndex() is { } selectedIndex)
        {
            NavigationState?.SetCurrent(_entryTable!, selectedIndex, resetSelectionAnchor: true);
        }

        PublishSelectionChanged();
    }

    private bool ExtendSelection(VirtualKey key)
    {
        if (!EnsureTable() || NavigationState is null || _entryTable!.Items.Count == 0)
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
            NavigationState.SetCurrent(_entryTable!, currentIndex.Value, resetSelectionAnchor: true);
        }

        var anchorIndex = NavigationState.GetSelectionAnchorIndex(_entryTable!) ?? currentIndex.Value;
        var cursorIndex = NavigationState.GetSelectionCursorIndex(_entryTable!) ?? currentIndex.Value;
        if (!FileEntryTableBehaviorHelper.TryGetRangeTargetIndex(_entryTable!, key, cursorIndex, out var targetIndex))
        {
            return false;
        }

        ApplySelectionRange(anchorIndex, targetIndex);
        return true;
    }

    private void ApplySelectionRange(int anchorIndex, int targetIndex)
    {
        if (_entryTable is null)
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
            _entryTable.SelectedItems.Clear();
            for (var i = startIndex; i <= endIndex; i++)
            {
                _entryTable.SelectedItems.Add(_entryTable.Items[i]);
            }
        }
        finally
        {
            _syncingSelection = false;
        }

        _shiftRangeActive = true;
        NavigationState?.SetSelectionRange(_entryTable, anchorIndex, targetIndex);
        _entryTable.ScrollRowIntoView(targetIndex);
        PublishSelectionChanged();
    }

    private void PublishSelectionChanged()
    {
        if (AssociatedObject is null || _entryTable is null)
        {
            return;
        }

        var selectedRows = _entryTable.SelectedItems
            .OfType<SpecFileEntryViewModel>()
            .ToList();
        var selectedItems = selectedRows
            .Where(static item => !SpecFileEntryViewModel.IsParentEntry(item))
            .ToList();
        var activeItem = NavigationState?.GetSelectionCursorIndex(_entryTable) is { } cursorIndex
            ? _entryTable.Items[cursorIndex] as SpecFileEntryViewModel
            : NavigationState?.GetCurrentItem(_entryTable) ?? _entryTable.SelectedItem as SpecFileEntryViewModel;

        WeakReferenceMessenger.Default.Send(
            new FileTableSelectionChangedMessage(
                AssociatedObject.Identity,
                selectedItems,
                selectedRows.Any(static item => SpecFileEntryViewModel.IsParentEntry(item)),
                activeItem));
    }

    private int? GetCurrentIndex() =>
        (_entryTable is not null ? NavigationState?.GetSelectionCursorIndex(_entryTable) : null)
        ?? (_entryTable is not null ? NavigationState?.GetCurrentIndex(_entryTable) : null)
        ?? GetCurrentSelectedIndex()
        ?? (_entryTable?.Items.Count > 0 ? 0 : null);

    private int? GetCurrentSelectedIndex()
    {
        if (_entryTable is null)
        {
            return null;
        }

        if (_entryTable.SelectedIndex >= 0)
        {
            return _entryTable.SelectedIndex;
        }

        if (FileEntryTableBehaviorHelper.GetRowIndex(
                _entryTable,
                _entryTable.SelectedItem as SpecFileEntryViewModel) is { } selectedItemIndex)
        {
            return selectedItemIndex;
        }

        foreach (var item in _entryTable.SelectedItems.OfType<SpecFileEntryViewModel>().Reverse())
        {
            if (FileEntryTableBehaviorHelper.GetRowIndex(_entryTable, item) is { } selectedIndex)
            {
                return selectedIndex;
            }
        }

        return null;
    }

    private int? ClampIndex(int? index)
    {
        if (_entryTable is null)
        {
            return null;
        }

        return FileEntryTableBehaviorHelper.ClampIndex(_entryTable, index);
    }

    private bool EnsureTable()
    {
        return FileEntryTableBehaviorHelper.EnsureTable(
            AssociatedObject,
            ref _entryTable,
            DetachTableEvents,
            AttachTableEvents);
    }

    private void OnSelectedItemsRequested(object recipient, FileTableSelectedItemsRequestMessage message)
    {
        if (AssociatedObject is null || message.Identity != AssociatedObject.Identity)
        {
            return;
        }

        message.Reply(GetSelectedItemsSnapshot());
    }

    private void AttachTableEvents()
    {
        if (_selectionEventsAttached || _entryTable is null)
        {
            return;
        }

        _entryTable.PreviewKeyDown += EntryTable_PreviewKeyDown;
        _entryTable.SelectionChanged += EntryTable_SelectionChanged;
        _selectionEventsAttached = true;
    }

    private void DetachTableEvents()
    {
        if (!_selectionEventsAttached)
        {
            return;
        }

        if (_entryTable is not null)
        {
            _entryTable.PreviewKeyDown -= EntryTable_PreviewKeyDown;
            _entryTable.SelectionChanged -= EntryTable_SelectionChanged;
        }

        _selectionEventsAttached = false;
    }

    private IReadOnlyList<SpecFileEntryViewModel> GetSelectedItemsSnapshot()
    {
        if (_entryTable is null)
        {
            return [];
        }

        return _entryTable.SelectedItems
            .OfType<SpecFileEntryViewModel>()
            .Where(static item => !SpecFileEntryViewModel.IsParentEntry(item))
            .ToList();
    }
}
