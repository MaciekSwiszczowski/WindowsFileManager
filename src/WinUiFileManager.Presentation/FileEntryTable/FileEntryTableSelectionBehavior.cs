using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Xaml.Interactivity;
using WinUiFileManager.Presentation.FileEntryTable.Messages;

namespace WinUiFileManager.Presentation.FileEntryTable;

public sealed class FileEntryTableSelectionBehavior : Behavior<SpecFileEntryTableView>
{
    private const string EntryTableName = "EntryTable";

    private readonly List<SpecFileEntryViewModel> _lastPublishedSelection = [];

    private TableView? _entryTable;
    private HashSet<string>? _pendingSelectedNames;
    private bool _isObservingKeyboardMessages;
    private bool _lastPublishedParentRowSelected;
    private SpecFileEntryViewModel? _lastPublishedActiveItem;
    private bool _pendingParentSelected;
    private bool _selectionEventsAttached;
    private bool _syncingSelection;
    private bool _syncVisibleSelectionQueued;
    private int? _selectionAnchorIndex;
    private int? _selectionCursorIndex;

    protected override void OnAttached()
    {
        base.OnAttached();

        AssociatedObject.Loaded += OnLoaded;
        AssociatedObject.GotFocus += OnGotFocus;
        AssociatedObject.LostFocus += OnLostFocus;
        AssociatedObject.VisibleItems.CollectionChanged += VisibleItems_CollectionChanged;
        WeakReferenceMessenger.Default.Register<FileTableFocusedMessage>(this, OnFileTableFocused);
    }

    protected override void OnDetaching()
    {
        StopObservingKeyboardMessages();
        DetachSelectionEvents();

        if (AssociatedObject is { } view)
        {
            view.Loaded -= OnLoaded;
            view.GotFocus -= OnGotFocus;
            view.LostFocus -= OnLostFocus;
            view.VisibleItems.CollectionChanged -= VisibleItems_CollectionChanged;
        }

        WeakReferenceMessenger.Default.Unregister<FileTableFocusedMessage>(this);
        _entryTable = null;
        _syncVisibleSelectionQueued = false;

        base.OnDetaching();
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => EnsureTable();

    private void OnGotFocus(object sender, RoutedEventArgs e)
    {
        if (AssociatedObject is null)
        {
            return;
        }

        WeakReferenceMessenger.Default.Send(new FileTableFocusedMessage(AssociatedObject.Identity));
        StartObservingKeyboardMessages();
    }

    private void OnLostFocus(object sender, RoutedEventArgs e) => StopObservingKeyboardMessages();

    private void OnFileTableFocused(object recipient, FileTableFocusedMessage message)
    {
        if (AssociatedObject is null || message.Identity == AssociatedObject.Identity)
        {
            return;
        }

        StopObservingKeyboardMessages();
    }

    private void StartObservingKeyboardMessages()
    {
        if (_isObservingKeyboardMessages)
        {
            return;
        }

        _isObservingKeyboardMessages = true;
        WeakReferenceMessenger.Default.Register<MoveCursorUpMessage>(this, (_, _) => MoveCursor(-1));
        WeakReferenceMessenger.Default.Register<MoveCursorDownMessage>(this, (_, _) => MoveCursor(1));
        WeakReferenceMessenger.Default.Register<MoveCursorPageUpMessage>(this, (_, _) => MoveCursorByPage(-1));
        WeakReferenceMessenger.Default.Register<MoveCursorPageDownMessage>(this, (_, _) => MoveCursorByPage(1));
        WeakReferenceMessenger.Default.Register<MoveCursorHomeMessage>(this, (_, _) => MoveToBoundary(moveToEnd: false));
        WeakReferenceMessenger.Default.Register<MoveCursorEndMessage>(this, (_, _) => MoveToBoundary(moveToEnd: true));
        WeakReferenceMessenger.Default.Register<ExtendSelectionUpMessage>(this, (_, _) => ExtendSelection(-1));
        WeakReferenceMessenger.Default.Register<ExtendSelectionDownMessage>(this, (_, _) => ExtendSelection(1));
        WeakReferenceMessenger.Default.Register<ExtendSelectionPageUpMessage>(this, (_, _) => ExtendSelectionByPage(-1));
        WeakReferenceMessenger.Default.Register<ExtendSelectionPageDownMessage>(this, (_, _) => ExtendSelectionByPage(1));
        WeakReferenceMessenger.Default.Register<ExtendSelectionHomeMessage>(this, (_, _) => ExtendSelectionToBoundary(moveToEnd: false));
        WeakReferenceMessenger.Default.Register<ExtendSelectionEndMessage>(this, (_, _) => ExtendSelectionToBoundary(moveToEnd: true));
        WeakReferenceMessenger.Default.Register<ToggleSelectionAtCursorMessage>(this, (_, _) => ToggleSelectionAtCursor(advance: false));
        WeakReferenceMessenger.Default.Register<ToggleSelectionAtCursorAndAdvanceMessage>(this, (_, _) => ToggleSelectionAtCursor(advance: true));
        WeakReferenceMessenger.Default.Register<SelectAllMessage>(this, (_, _) => SelectAllVisibleRows());
        WeakReferenceMessenger.Default.Register<ClearSelectionMessage>(this, (_, _) => ClearSelection());
        WeakReferenceMessenger.Default.Register<ActivateInvokedMessage>(this, (_, _) => ActivateCurrentParentRow());
    }

    private void StopObservingKeyboardMessages()
    {
        if (!_isObservingKeyboardMessages)
        {
            return;
        }

        _isObservingKeyboardMessages = false;
        WeakReferenceMessenger.Default.Unregister<MoveCursorUpMessage>(this);
        WeakReferenceMessenger.Default.Unregister<MoveCursorDownMessage>(this);
        WeakReferenceMessenger.Default.Unregister<MoveCursorPageUpMessage>(this);
        WeakReferenceMessenger.Default.Unregister<MoveCursorPageDownMessage>(this);
        WeakReferenceMessenger.Default.Unregister<MoveCursorHomeMessage>(this);
        WeakReferenceMessenger.Default.Unregister<MoveCursorEndMessage>(this);
        WeakReferenceMessenger.Default.Unregister<ExtendSelectionUpMessage>(this);
        WeakReferenceMessenger.Default.Unregister<ExtendSelectionDownMessage>(this);
        WeakReferenceMessenger.Default.Unregister<ExtendSelectionPageUpMessage>(this);
        WeakReferenceMessenger.Default.Unregister<ExtendSelectionPageDownMessage>(this);
        WeakReferenceMessenger.Default.Unregister<ExtendSelectionHomeMessage>(this);
        WeakReferenceMessenger.Default.Unregister<ExtendSelectionEndMessage>(this);
        WeakReferenceMessenger.Default.Unregister<ToggleSelectionAtCursorMessage>(this);
        WeakReferenceMessenger.Default.Unregister<ToggleSelectionAtCursorAndAdvanceMessage>(this);
        WeakReferenceMessenger.Default.Unregister<SelectAllMessage>(this);
        WeakReferenceMessenger.Default.Unregister<ClearSelectionMessage>(this);
        WeakReferenceMessenger.Default.Unregister<ActivateInvokedMessage>(this);
    }

    private void VisibleItems_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (!EnsureTable())
        {
            return;
        }

        _pendingSelectedNames ??= _entryTable!.SelectedItems
            .OfType<SpecFileEntryViewModel>()
            .Where(static item => !IsParentRow(item))
            .Select(static item => item.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        _pendingParentSelected = _entryTable!.SelectedItems.OfType<SpecFileEntryViewModel>().Any(IsParentRow);

        QueueVisibleSelectionSync();
    }

    private void QueueVisibleSelectionSync()
    {
        if (_syncVisibleSelectionQueued || AssociatedObject is null)
        {
            return;
        }

        _syncVisibleSelectionQueued = true;
        AssociatedObject.DispatcherQueue.TryEnqueue(() =>
        {
            _syncVisibleSelectionQueued = false;
            SyncSelectionWithVisibleItems();
        });
    }

    private void SyncSelectionWithVisibleItems()
    {
        if (!EnsureTable() || AssociatedObject is null)
        {
            return;
        }

        var selectedNames = _pendingSelectedNames ?? [];
        var parentSelected = _pendingParentSelected;
        _pendingSelectedNames = null;
        _pendingParentSelected = false;

        _syncingSelection = true;
        try
        {
            _entryTable!.SelectedItems.Clear();
            foreach (var item in AssociatedObject.VisibleItems)
            {
                if (IsParentRow(item) ? parentSelected : selectedNames.Contains(item.Name))
                {
                    _entryTable.SelectedItems.Add(item);
                }
            }

            _selectionCursorIndex = ClampSelectionIndex(_selectionCursorIndex);
            _entryTable.SelectedItem = _selectionCursorIndex is { } cursorIndex
                ? AssociatedObject.VisibleItems[cursorIndex]
                : _entryTable.SelectedItems.OfType<SpecFileEntryViewModel>().LastOrDefault();
        }
        finally
        {
            _syncingSelection = false;
        }

        PublishSelectionChangedIfNeeded();
    }

    private void EntryTable_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!EnsureTable() || _syncingSelection)
        {
            return;
        }

        if (GetRowIndex(e.AddedItems.OfType<SpecFileEntryViewModel>().LastOrDefault() ?? _entryTable!.SelectedItem as SpecFileEntryViewModel) is { } index)
        {
            _selectionAnchorIndex = index;
            _selectionCursorIndex = index;
        }
        else if (_entryTable!.SelectedItems.Count == 0)
        {
            _selectionAnchorIndex = null;
            _selectionCursorIndex = null;
        }

        PublishSelectionChangedIfNeeded();
    }

    private void MoveCursor(int delta)
    {
        var currentIndex = GetCurrentSelectionIndex();
        if (currentIndex is null)
        {
            SelectFirstAvailableRow();
            return;
        }

        SelectRowIndex(ClampSelectionIndex(currentIndex.Value + delta));
    }

    private void MoveCursorByPage(int direction)
    {
        if (direction == 0)
        {
            return;
        }

        var currentIndex = GetCurrentSelectionIndex();
        if (currentIndex is null)
        {
            SelectFirstAvailableRow();
            return;
        }

        SelectRowIndex(ClampSelectionIndex(currentIndex.Value + (direction * GetVisibleRowCount())));
    }

    private void MoveToBoundary(bool moveToEnd)
    {
        if (AssociatedObject is not { VisibleItems.Count: > 0 } view)
        {
            return;
        }

        SelectRowIndex(moveToEnd ? view.VisibleItems.Count - 1 : 0);
    }

    private void SelectFirstAvailableRow()
    {
        if (AssociatedObject is { VisibleItems.Count: > 0 })
        {
            SelectRowIndex(0);
        }
    }

    private void SelectRowIndex(int? index)
    {
        if (!EnsureTable() || AssociatedObject is not { } view || index is null || index < 0 || index >= view.VisibleItems.Count)
        {
            return;
        }

        var item = view.VisibleItems[index.Value];
        _syncingSelection = true;
        try
        {
            _entryTable!.SelectedItems.Clear();
            _entryTable.SelectedItems.Add(item);
            _entryTable.SelectedItem = item;
            _selectionAnchorIndex = index;
            _selectionCursorIndex = index;
        }
        finally
        {
            _syncingSelection = false;
        }

        _entryTable.ScrollRowIntoView(index.Value);
        _entryTable.Focus(FocusState.Programmatic);
        PublishSelectionChangedIfNeeded();
    }

    private void SelectAllVisibleRows()
    {
        if (!EnsureTable() || AssociatedObject is not { } view)
        {
            return;
        }

        _syncingSelection = true;
        try
        {
            _entryTable!.SelectedItems.Clear();
            foreach (var item in view.VisibleItems)
            {
                _entryTable.SelectedItems.Add(item);
            }

            _selectionAnchorIndex = view.VisibleItems.Count > 0 ? 0 : null;
            _selectionCursorIndex = view.VisibleItems.Count > 0 ? view.VisibleItems.Count - 1 : null;
            _entryTable.SelectedItem = _selectionCursorIndex is { } cursorIndex ? view.VisibleItems[cursorIndex] : null;
        }
        finally
        {
            _syncingSelection = false;
        }

        PublishSelectionChangedIfNeeded();
    }

    private void ClearSelection()
    {
        if (!EnsureTable())
        {
            return;
        }

        _syncingSelection = true;
        try
        {
            _entryTable!.SelectedItems.Clear();
            _entryTable.SelectedItem = null;
            _selectionAnchorIndex = null;
            _selectionCursorIndex = null;
        }
        finally
        {
            _syncingSelection = false;
        }

        PublishSelectionChangedIfNeeded();
    }

    private void ToggleSelectionAtCursor(bool advance)
    {
        if (!EnsureTable() || AssociatedObject is not { } view)
        {
            return;
        }

        var index = GetCurrentSelectionIndex();
        if (index is null)
        {
            SelectFirstAvailableRow();
            index = GetCurrentSelectionIndex();
        }

        if (index is null || index < 0 || index >= view.VisibleItems.Count)
        {
            return;
        }

        var item = view.VisibleItems[index.Value];
        _syncingSelection = true;
        try
        {
            if (_entryTable!.SelectedItems.Contains(item))
            {
                _entryTable.SelectedItems.Remove(item);
            }
            else
            {
                _entryTable.SelectedItems.Add(item);
            }

            _entryTable.SelectedItem = item;
            _selectionAnchorIndex = index;
            _selectionCursorIndex = index;
        }
        finally
        {
            _syncingSelection = false;
        }

        PublishSelectionChangedIfNeeded();

        if (advance && index.Value < view.VisibleItems.Count - 1)
        {
            _selectionCursorIndex = index.Value + 1;
            _entryTable!.SelectedItem = view.VisibleItems[_selectionCursorIndex.Value];
            _entryTable.ScrollRowIntoView(_selectionCursorIndex.Value);
            PublishSelectionChangedIfNeeded();
        }
    }

    private void ExtendSelection(int delta)
    {
        if (delta == 0 || EnsureSelectionAnchor() is not { } anchor)
        {
            return;
        }

        var currentIndex = _selectionCursorIndex ?? anchor;
        ApplySelectionRange(anchor, ClampSelectionIndex(currentIndex + delta));
    }

    private void ExtendSelectionByPage(int direction)
    {
        if (direction == 0 || EnsureSelectionAnchor() is not { } anchor)
        {
            return;
        }

        var currentIndex = _selectionCursorIndex ?? anchor;
        ApplySelectionRange(anchor, ClampSelectionIndex(currentIndex + (direction * GetVisibleRowCount())));
    }

    private void ExtendSelectionToBoundary(bool moveToEnd)
    {
        if (EnsureSelectionAnchor() is not { } anchor || AssociatedObject is not { VisibleItems.Count: > 0 } view)
        {
            return;
        }

        ApplySelectionRange(anchor, moveToEnd ? view.VisibleItems.Count - 1 : 0);
    }

    private int? EnsureSelectionAnchor()
    {
        if (_selectionAnchorIndex is { } anchor)
        {
            return ClampSelectionIndex(anchor);
        }

        var currentIndex = GetCurrentSelectionIndex();
        if (currentIndex is null)
        {
            SelectFirstAvailableRow();
            currentIndex = GetCurrentSelectionIndex();
        }

        _selectionAnchorIndex = currentIndex;
        _selectionCursorIndex = currentIndex;
        return currentIndex;
    }

    private void ApplySelectionRange(int anchorIndex, int? cursorIndex)
    {
        if (!EnsureTable() || AssociatedObject is not { } view || cursorIndex is null || view.VisibleItems.Count == 0)
        {
            return;
        }

        anchorIndex = ClampSelectionIndex(anchorIndex) ?? 0;
        var targetIndex = cursorIndex.Value;
        var startIndex = Math.Min(anchorIndex, targetIndex);
        var endIndex = Math.Max(anchorIndex, targetIndex);

        _syncingSelection = true;
        try
        {
            _entryTable!.SelectedItems.Clear();
            for (var i = startIndex; i <= endIndex && i < view.VisibleItems.Count; i++)
            {
                _entryTable.SelectedItems.Add(view.VisibleItems[i]);
            }

            _entryTable.SelectedItem = view.VisibleItems[targetIndex];
            _selectionAnchorIndex = anchorIndex;
            _selectionCursorIndex = targetIndex;
        }
        finally
        {
            _syncingSelection = false;
        }

        _entryTable.ScrollRowIntoView(targetIndex);
        _entryTable.Focus(FocusState.Programmatic);
        PublishSelectionChangedIfNeeded();
    }

    private int? ClampSelectionIndex(int? index)
    {
        if (AssociatedObject is not { VisibleItems.Count: > 0 } view || index is null)
        {
            return null;
        }

        return Math.Clamp(index.Value, 0, view.VisibleItems.Count - 1);
    }

    private int? GetCurrentSelectionIndex()
    {
        if (!EnsureTable())
        {
            return null;
        }

        if (ClampSelectionIndex(_selectionCursorIndex) is { } cursorIndex)
        {
            return cursorIndex;
        }

        return GetRowIndex(_entryTable!.SelectedItem as SpecFileEntryViewModel)
            ?? GetLastSelectedRowIndex();
    }

    private int? GetLastSelectedRowIndex()
    {
        if (!EnsureTable())
        {
            return null;
        }

        int? lastIndex = null;
        foreach (var item in _entryTable!.SelectedItems.OfType<SpecFileEntryViewModel>())
        {
            if (GetRowIndex(item) is { } index)
            {
                lastIndex = index;
            }
        }

        return lastIndex;
    }

    private int? GetRowIndex(SpecFileEntryViewModel? item)
    {
        if (item is null || AssociatedObject is null)
        {
            return null;
        }

        var index = AssociatedObject.VisibleItems.IndexOf(item);
        return index >= 0 ? index : null;
    }

    private int GetVisibleRowCount() =>
        _entryTable is null
            ? 1
            : Math.Max(1, (int)(_entryTable.ActualHeight / Math.Max(_entryTable.RowHeight, 1d)) - 1);

    private void ActivateCurrentParentRow()
    {
        if (AssociatedObject is null || GetActiveItem() is not { } activeItem || !IsParentRow(activeItem))
        {
            return;
        }

        var selectedEntries = GetSelectedEntries();
        if (selectedEntries.Count > 0)
        {
            return;
        }

        WeakReferenceMessenger.Default.Send(new FileTableNavigateUpRequestedMessage(AssociatedObject.Identity));
    }

    private SpecFileEntryViewModel? GetActiveItem()
    {
        if (AssociatedObject is null)
        {
            return null;
        }

        return ClampSelectionIndex(_selectionCursorIndex) is { } cursorIndex
            ? AssociatedObject.VisibleItems[cursorIndex]
            : _entryTable?.SelectedItem as SpecFileEntryViewModel;
    }

    private List<SpecFileEntryViewModel> GetSelectedEntries()
    {
        if (!EnsureTable() || AssociatedObject is null)
        {
            return [];
        }

        var selectedItems = _entryTable!.SelectedItems
            .OfType<SpecFileEntryViewModel>()
            .Where(static item => !IsParentRow(item))
            .ToHashSet();

        return [.. AssociatedObject.VisibleItems.Where(selectedItems.Contains)];
    }

    private bool IsParentRowSelected =>
        _entryTable?.SelectedItems.OfType<SpecFileEntryViewModel>().Any(IsParentRow) == true;

    private void PublishSelectionChangedIfNeeded()
    {
        if (AssociatedObject is null)
        {
            return;
        }

        var selectedEntries = GetSelectedEntries();
        var isParentRowSelected = IsParentRowSelected;
        var activeItem = GetActiveItem();
        if (_lastPublishedParentRowSelected == isParentRowSelected
            && _lastPublishedSelection.SequenceEqual(selectedEntries)
            && ReferenceEquals(_lastPublishedActiveItem, activeItem))
        {
            return;
        }

        _lastPublishedParentRowSelected = isParentRowSelected;
        _lastPublishedActiveItem = activeItem;
        _lastPublishedSelection.Clear();
        _lastPublishedSelection.AddRange(selectedEntries);

        WeakReferenceMessenger.Default.Send(
            new FileTableSelectionChangedMessage(
                AssociatedObject.Identity,
                selectedEntries,
                isParentRowSelected,
                activeItem));
    }

    private bool EnsureTable()
    {
        if (AssociatedObject is null)
        {
            return false;
        }

        _entryTable ??= AssociatedObject.FindDescendant<TableView>(static table => table.Name == EntryTableName);
        if (_entryTable is null)
        {
            return false;
        }

        AttachSelectionEvents();
        return true;
    }

    private void AttachSelectionEvents()
    {
        if (_selectionEventsAttached || _entryTable is null)
        {
            return;
        }

        _entryTable.SelectionChanged += EntryTable_SelectionChanged;
        _selectionEventsAttached = true;
    }

    private void DetachSelectionEvents()
    {
        if (!_selectionEventsAttached)
        {
            return;
        }

        _entryTable?.SelectionChanged -= EntryTable_SelectionChanged;

        _selectionEventsAttached = false;
    }

    private static bool IsParentRow(SpecFileEntryViewModel item) =>
        item.Model is null;
}
