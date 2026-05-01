using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Xaml.Interactivity;
using WinUiFileManager.Presentation.FileEntryTable.Messages;

namespace WinUiFileManager.Presentation.FileEntryTable;

public sealed class FileEntryTableSelectionBehavior : Behavior<SpecFileEntryTableView>
{
    private const int ParentRowIndex = -1;
    private const string ParentTableName = "ParentTable";
    private const string EntryTableName = "EntryTable";

    private readonly List<SpecFileEntryViewModel> _lastPublishedSelection = [];
    private readonly ObservableCollection<SpecFileEntryViewModel> _selectedItems = [];

    private TableView? _parentTable;
    private TableView? _entryTable;
    private HashSet<string>? _pendingSelectedNames;
    private bool _isObservingKeyboardMessages;
    private bool _parentSelectionPublishQueued;
    private bool _selectionEventsAttached;
    private bool _syncingSelection;
    private bool _syncVisibleSelectionQueued;
    private bool _lastPublishedParentRowSelected;
    private int? _selectionAnchorIndex;
    private int? _selectionCursorIndex;

    protected override void OnAttached()
    {
        base.OnAttached();

        AssociatedObject.Loaded += OnLoaded;
        AssociatedObject.GotFocus += OnGotFocus;
        AssociatedObject.LostFocus += OnLostFocus;
        AssociatedObject.ParentRows.CollectionChanged += ParentRows_CollectionChanged;
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
            view.ParentRows.CollectionChanged -= ParentRows_CollectionChanged;
            view.VisibleItems.CollectionChanged -= VisibleItems_CollectionChanged;
        }

        WeakReferenceMessenger.Default.Unregister<FileTableFocusedMessage>(this);
        _parentTable = null;
        _entryTable = null;
        _parentSelectionPublishQueued = false;
        _syncVisibleSelectionQueued = false;

        base.OnDetaching();
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => EnsureTables();

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
        WeakReferenceMessenger.Default.Unregister<SelectAllMessage>(this);
        WeakReferenceMessenger.Default.Unregister<ClearSelectionMessage>(this);
        WeakReferenceMessenger.Default.Unregister<ActivateInvokedMessage>(this);
    }

    private void ParentRows_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (AssociatedObject?.IsRootContent == true && EnsureTables())
        {
            _parentTable!.SelectedItem = null;
        }

        QueueParentSelectionPublish();
    }

    private void QueueParentSelectionPublish()
    {
        if (_parentSelectionPublishQueued || AssociatedObject is null)
        {
            return;
        }

        _parentSelectionPublishQueued = true;
        AssociatedObject.DispatcherQueue.TryEnqueue(() =>
        {
            _parentSelectionPublishQueued = false;
            PublishSelectionChangedIfNeeded();
        });
    }

    private void VisibleItems_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _pendingSelectedNames ??= _selectedItems
            .Select(static item => item.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

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
        if (AssociatedObject is null)
        {
            return;
        }

        var selectedNames = _pendingSelectedNames ?? [];
        _pendingSelectedNames = null;

        _selectedItems.Clear();
        foreach (var item in AssociatedObject.VisibleItems.Where(item => selectedNames.Contains(item.Name)))
        {
            _selectedItems.Add(item);
        }

        PublishSelectionChangedIfNeeded();
    }

    private void ParentTable_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!EnsureTables() || _syncingSelection || _parentTable!.SelectedItem is null)
        {
            return;
        }

        _syncingSelection = true;
        try
        {
            _entryTable!.SelectedItems.Clear();
            _entryTable.SelectedItem = null;
            _selectedItems.Clear();
            _selectionAnchorIndex = ParentRowIndex;
            _selectionCursorIndex = ParentRowIndex;
        }
        finally
        {
            _syncingSelection = false;
        }

        PublishSelectionChangedIfNeeded();
        _parentTable.Focus(FocusState.Programmatic);
    }

    private void EntryTable_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!EnsureTables() || _syncingSelection)
        {
            return;
        }

        _syncingSelection = true;
        try
        {
            var parentWasSelected = _parentTable!.SelectedItem is not null;
            _parentTable.SelectedItem = null;

            SyncSelectedItemsFromEntryTable();
            if (GetBodySelectionIndex(e.AddedItems.OfType<SpecFileEntryViewModel>().LastOrDefault()) is { } index)
            {
                _selectionAnchorIndex = parentWasSelected ? ParentRowIndex : index;
                _selectionCursorIndex = index;
            }
        }
        finally
        {
            _syncingSelection = false;
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

        var nextIndex = currentIndex.Value + delta;

        if (nextIndex < 0 && HasParentRow)
        {
            SelectParentRow();
            return;
        }

        if (AssociatedObject is not null && nextIndex >= 0 && nextIndex < AssociatedObject.VisibleItems.Count)
        {
            SelectBodyIndex(nextIndex);
        }
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

        var targetIndex = currentIndex.Value + (direction * GetVisibleBodyRowCount());
        if (direction < 0 && targetIndex < 0 && HasParentRow)
        {
            SelectParentRow();
            return;
        }

        if (AssociatedObject is { VisibleItems.Count: > 0 } view)
        {
            SelectBodyIndex(Math.Clamp(targetIndex, 0, view.VisibleItems.Count - 1));
        }
    }

    private void MoveToBoundary(bool moveToEnd)
    {
        if (AssociatedObject is not { } view)
        {
            return;
        }

        if (moveToEnd)
        {
            if (view.VisibleItems.Count > 0)
            {
                SelectBodyIndex(view.VisibleItems.Count - 1);
            }
            else if (!view.IsRootContent)
            {
                SelectParentRow();
            }

            return;
        }

        if (!view.IsRootContent)
        {
            SelectParentRow();
        }
        else if (view.VisibleItems.Count > 0)
        {
            SelectBodyIndex(0);
        }
    }

    private void SelectParentRow()
    {
        if (!EnsureTables() || AssociatedObject is not { ParentRows.Count: > 0 } view)
        {
            return;
        }

        _syncingSelection = true;
        try
        {
            _entryTable!.SelectedItems.Clear();
            _entryTable.SelectedItem = null;
            _parentTable!.SelectedItem = view.ParentRows[0];
            _selectedItems.Clear();
            _selectionAnchorIndex = ParentRowIndex;
            _selectionCursorIndex = ParentRowIndex;
        }
        finally
        {
            _syncingSelection = false;
        }

        _parentTable.Focus(FocusState.Programmatic);
        PublishSelectionChangedIfNeeded();
    }

    private void SelectBodyIndex(int index)
    {
        if (!EnsureTables() || AssociatedObject is not { } view || index < 0 || index >= view.VisibleItems.Count)
        {
            return;
        }

        var item = view.VisibleItems[index];
        _syncingSelection = true;
        try
        {
            _parentTable!.SelectedItem = null;
            _entryTable!.SelectedItems.Clear();
            _entryTable.SelectedItems.Add(item);
            _entryTable.SelectedItem = item;
            _selectedItems.Clear();
            _selectedItems.Add(item);
            _selectionAnchorIndex = index;
            _selectionCursorIndex = index;
        }
        finally
        {
            _syncingSelection = false;
        }

        _entryTable.ScrollRowIntoView(index);
        _entryTable.Focus(FocusState.Programmatic);
        PublishSelectionChangedIfNeeded();
    }

    private void SelectAllVisibleRows()
    {
        if (!EnsureTables() || AssociatedObject is null)
        {
            return;
        }

        _syncingSelection = true;
        try
        {
            _parentTable!.SelectedItem = null;
            _entryTable!.SelectedItems.Clear();
            _selectedItems.Clear();
            foreach (var item in AssociatedObject.VisibleItems)
            {
                _entryTable.SelectedItems.Add(item);
                _selectedItems.Add(item);
            }

            _selectionAnchorIndex = AssociatedObject.VisibleItems.Count > 0 ? 0 : null;
            _selectionCursorIndex = AssociatedObject.VisibleItems.Count > 0
                ? AssociatedObject.VisibleItems.Count - 1
                : null;
        }
        finally
        {
            _syncingSelection = false;
        }

        PublishSelectionChangedIfNeeded();
    }

    private void ClearSelection()
    {
        if (!EnsureTables())
        {
            return;
        }

        _syncingSelection = true;
        try
        {
            _parentTable!.SelectedItem = null;
            _entryTable!.SelectedItems.Clear();
            _entryTable.SelectedItem = null;
            _selectedItems.Clear();
            _selectionAnchorIndex = null;
            _selectionCursorIndex = null;
        }
        finally
        {
            _syncingSelection = false;
        }

        PublishSelectionChangedIfNeeded();
    }

    private void ExtendSelection(int delta)
    {
        if (delta == 0)
        {
            return;
        }

        if (EnsureSelectionAnchor() is not { } anchor)
        {
            return;
        }

        var currentIndex = _selectionCursorIndex ?? anchor;
        var targetIndex = ClampSelectionIndex(currentIndex + delta);
        ApplySelectionRange(anchor, targetIndex);
    }

    private void ExtendSelectionByPage(int direction)
    {
        if (direction == 0)
        {
            return;
        }

        if (EnsureSelectionAnchor() is not { } anchor)
        {
            return;
        }

        var currentIndex = _selectionCursorIndex ?? anchor;
        var targetIndex = ClampSelectionIndex(currentIndex + (direction * GetVisibleBodyRowCount()));
        ApplySelectionRange(anchor, targetIndex);
    }

    private void ExtendSelectionToBoundary(bool moveToEnd)
    {
        if (EnsureSelectionAnchor() is not { } anchor || AssociatedObject is null)
        {
            return;
        }

        var targetIndex = moveToEnd
            ? AssociatedObject.VisibleItems.Count - 1
            : HasParentRow ? ParentRowIndex : 0;

        ApplySelectionRange(anchor, ClampSelectionIndex(targetIndex));
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

    private void ApplySelectionRange(int anchorIndex, int cursorIndex)
    {
        if (!EnsureTables() || AssociatedObject is not { } view ||
            view.ParentRows.Count == 0 && view.VisibleItems.Count == 0)
        {
            return;
        }

        anchorIndex = ClampSelectionIndex(anchorIndex);
        cursorIndex = ClampSelectionIndex(cursorIndex);

        var startIndex = Math.Min(anchorIndex, cursorIndex);
        var endIndex = Math.Max(anchorIndex, cursorIndex);
        var includesParent = HasParentRow && startIndex <= ParentRowIndex && endIndex >= ParentRowIndex;

        _syncingSelection = true;
        try
        {
            _parentTable!.SelectedItem = includesParent ? view.ParentRows[0] : null;
            _entryTable!.SelectedItems.Clear();
            _selectedItems.Clear();

            for (var i = Math.Max(0, startIndex); i <= endIndex && i < view.VisibleItems.Count; i++)
            {
                var item = view.VisibleItems[i];
                _entryTable.SelectedItems.Add(item);
                _selectedItems.Add(item);
            }

            _entryTable.SelectedItem =
                cursorIndex >= 0 && cursorIndex < view.VisibleItems.Count
                    ? view.VisibleItems[cursorIndex]
                    : null;

            _selectionAnchorIndex = anchorIndex;
            _selectionCursorIndex = cursorIndex;
        }
        finally
        {
            _syncingSelection = false;
        }

        if (cursorIndex == ParentRowIndex && includesParent)
        {
            _parentTable.Focus(FocusState.Programmatic);
        }
        else if (cursorIndex >= 0)
        {
            _entryTable.ScrollRowIntoView(cursorIndex);
            _entryTable.Focus(FocusState.Programmatic);
        }

        PublishSelectionChangedIfNeeded();
    }

    private int ClampSelectionIndex(int index)
    {
        if (AssociatedObject is null)
        {
            return index;
        }

        var minimumIndex = HasParentRow ? ParentRowIndex : 0;
        var maximumIndex = AssociatedObject.VisibleItems.Count > 0
            ? AssociatedObject.VisibleItems.Count - 1
            : minimumIndex;

        return Math.Clamp(index, minimumIndex, maximumIndex);
    }

    private int? GetCurrentSelectionIndex()
    {
        if (!EnsureTables())
        {
            return null;
        }

        if (_selectionCursorIndex is { } cursorIndex)
        {
            return ClampSelectionIndex(cursorIndex);
        }

        if (_parentTable!.SelectedItem is not null)
        {
            return ParentRowIndex;
        }

        return GetBodySelectionIndex(_entryTable!.SelectedItem as SpecFileEntryViewModel)
            ?? GetLastSelectedBodyIndex();
    }

    private int? GetLastSelectedBodyIndex()
    {
        if (!EnsureTables())
        {
            return null;
        }

        int? lastIndex = null;
        foreach (var item in _entryTable!.SelectedItems.OfType<SpecFileEntryViewModel>())
        {
            if (GetBodySelectionIndex(item) is { } index)
            {
                lastIndex = index;
            }
        }

        return lastIndex;
    }

    private int? GetBodySelectionIndex(SpecFileEntryViewModel? item)
    {
        if (item is null || AssociatedObject is null)
        {
            return null;
        }

        var index = AssociatedObject.VisibleItems.IndexOf(item);
        return index >= 0 ? index : null;
    }

    private void SelectFirstAvailableRow()
    {
        if (AssociatedObject is null)
        {
            return;
        }

        if (HasParentRow)
        {
            SelectParentRow();
        }
        else if (AssociatedObject.VisibleItems.Count > 0)
        {
            SelectBodyIndex(0);
        }
    }

    private int GetVisibleBodyRowCount() =>
        _entryTable is null
            ? 1
            : Math.Max(1, (int)(_entryTable.ActualHeight / Math.Max(_entryTable.RowHeight, 1d)) - 1);

    private bool HasParentRow =>
        AssociatedObject is { IsRootContent: false, ParentRows.Count: > 0 };

    private bool IsParentRowSelected => _parentTable?.SelectedItem is not null;

    private void ActivateCurrentParentRow()
    {
        if (AssociatedObject is null || _parentTable?.SelectedItem is null || _selectedItems.Count > 0)
        {
            return;
        }

        WeakReferenceMessenger.Default.Send(new FileTableNavigateUpRequestedMessage(AssociatedObject.Identity));
    }

    private void SyncSelectedItemsFromEntryTable()
    {
        if (AssociatedObject is null || !EnsureTables())
        {
            return;
        }

        var selectedItems = _entryTable!.SelectedItems
            .OfType<SpecFileEntryViewModel>()
            .ToHashSet();

        _selectedItems.Clear();
        foreach (var item in AssociatedObject.VisibleItems)
        {
            if (selectedItems.Contains(item))
            {
                _selectedItems.Add(item);
            }
        }
    }

    private void PublishSelectionChangedIfNeeded()
    {
        if (AssociatedObject is null)
        {
            return;
        }

        var isParentRowSelected = IsParentRowSelected;
        if (_lastPublishedParentRowSelected == isParentRowSelected
            && _lastPublishedSelection.SequenceEqual(_selectedItems))
        {
            return;
        }

        _lastPublishedParentRowSelected = isParentRowSelected;
        _lastPublishedSelection.Clear();
        _lastPublishedSelection.AddRange(_selectedItems);

        WeakReferenceMessenger.Default.Send(
            new FileTableSelectionChangedMessage(AssociatedObject.Identity, _selectedItems.ToList(), isParentRowSelected));
    }

    private bool EnsureTables()
    {
        if (AssociatedObject is null)
        {
            return false;
        }

        _parentTable ??= FindTable(ParentTableName);
        _entryTable ??= FindTable(EntryTableName);

        if (_parentTable is null || _entryTable is null)
        {
            return false;
        }

        AttachSelectionEvents();
        return true;
    }

    private TableView? FindTable(string name) =>
        AssociatedObject.FindDescendant<TableView>(table => table.Name == name);

    private void AttachSelectionEvents()
    {
        if (_selectionEventsAttached || _parentTable is null || _entryTable is null)
        {
            return;
        }

        _parentTable.SelectionChanged += ParentTable_SelectionChanged;
        _entryTable.SelectionChanged += EntryTable_SelectionChanged;
        _selectionEventsAttached = true;
    }

    private void DetachSelectionEvents()
    {
        if (!_selectionEventsAttached)
        {
            return;
        }

        if (_parentTable is not null)
        {
            _parentTable.SelectionChanged -= ParentTable_SelectionChanged;
        }

        if (_entryTable is not null)
        {
            _entryTable.SelectionChanged -= EntryTable_SelectionChanged;
        }

        _selectionEventsAttached = false;
    }
}
