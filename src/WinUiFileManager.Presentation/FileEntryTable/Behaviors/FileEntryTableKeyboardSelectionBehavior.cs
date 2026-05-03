using System.Reflection;

namespace WinUiFileManager.Presentation.FileEntryTable.Behaviors;

/// <summary>
/// Publishes table selection state and patches WinUI.TableView range extension for
/// Shift+Up/Down (extend one row), Shift+Home/End (extend to first/last row), and
/// Shift+PageUp/PageDown (extend by one visible page). Non-shift navigation and
/// selection gestures are left to the native TableView implementation.
/// </summary>
public sealed class FileEntryTableKeyboardSelectionBehavior : Behavior<SpecFileEntryTableView>
{
    private static readonly PropertyInfo? LastSelectionUnitProperty =
        typeof(TableView).GetProperty(
            "LastSelectionUnit",
            BindingFlags.NonPublic | BindingFlags.Instance);

    private static readonly PropertyInfo? CurrentRowIndexProperty =
        typeof(TableView).GetProperty(
            "CurrentRowIndex",
            BindingFlags.NonPublic | BindingFlags.Instance);

    private static readonly PropertyInfo? SelectionStartRowIndexProperty =
        typeof(TableView).GetProperty(
            "SelectionStartRowIndex",
            BindingFlags.NonPublic | BindingFlags.Instance);

    private static readonly PropertyInfo? SelectionStartCellSlotProperty =
        typeof(TableView).GetProperty(
            "SelectionStartCellSlot",
            BindingFlags.NonPublic | BindingFlags.Instance);

    private TableView? _entryTable;
    private bool _selectionEventsAttached;
    private bool _syncingSelection;
    private bool _shiftRangeActive;
    private int? _selectionAnchorIndex;
    private int? _selectionCursorIndex;

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
        if (e.Handled || !IsModifierDown(VirtualKey.Shift) || IsModifierDown(VirtualKey.Control))
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

        if (GetRowIndex(e.AddedItems.OfType<SpecFileEntryViewModel>().LastOrDefault()) is { } addedIndex)
        {
            _selectionAnchorIndex = addedIndex;
            _selectionCursorIndex = addedIndex;
            SyncNativeKeyboardState(addedIndex, addedIndex);
            PublishSelectionChanged();
            return;
        }

        if (_entryTable!.SelectedItems.Count == 0)
        {
            _selectionAnchorIndex = null;
            _selectionCursorIndex = null;
            PublishSelectionChanged();
            return;
        }

        if (GetCurrentSelectedIndex() is { } selectedIndex)
        {
            _selectionAnchorIndex = selectedIndex;
            _selectionCursorIndex = selectedIndex;
            SyncNativeKeyboardState(selectedIndex, selectedIndex);
        }

        PublishSelectionChanged();
    }

    private bool ExtendSelection(VirtualKey key)
    {
        if (!EnsureTable() || _entryTable!.Items.Count == 0)
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
            _selectionAnchorIndex = currentIndex;
            _selectionCursorIndex = currentIndex;
        }

        var anchorIndex = ClampIndex(_selectionAnchorIndex) ?? currentIndex.Value;
        var cursorIndex = ClampIndex(_selectionCursorIndex) ?? currentIndex.Value;
        if (!TryGetTargetIndex(key, cursorIndex, out var targetIndex))
        {
            return false;
        }

        ApplySelectionRange(anchorIndex, targetIndex);
        return true;
    }

    private bool TryGetTargetIndex(VirtualKey key, int cursorIndex, out int targetIndex)
    {
        targetIndex = key switch
        {
            VirtualKey.Up => cursorIndex - 1,
            VirtualKey.Down => cursorIndex + 1,
            VirtualKey.Home => 0,
            VirtualKey.End => _entryTable!.Items.Count - 1,
            VirtualKey.PageUp => cursorIndex - GetPageRowCount(),
            VirtualKey.PageDown => cursorIndex + GetPageRowCount(),
            _ => cursorIndex,
        };

        if (key is not (VirtualKey.Up
            or VirtualKey.Down
            or VirtualKey.Home
            or VirtualKey.End
            or VirtualKey.PageUp
            or VirtualKey.PageDown))
        {
            return false;
        }

        targetIndex = ClampIndex(targetIndex) ?? cursorIndex;
        return true;
    }

    private int GetPageRowCount()
    {
        if (_entryTable is null)
        {
            return 1;
        }

        var rowHeight = _entryTable.RowHeight;
        if (double.IsNaN(rowHeight) || rowHeight <= 0)
        {
            rowHeight = 32d;
        }

        return Math.Max(1, (int)Math.Floor(_entryTable.ActualHeight / rowHeight) - 1);
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
        _selectionAnchorIndex = anchorIndex;
        _selectionCursorIndex = targetIndex;
        SyncNativeKeyboardState(anchorIndex, targetIndex);
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
            .Where(static item => item.Model is not null)
            .ToList();
        var activeItem = ClampIndex(_selectionCursorIndex) is { } cursorIndex
            ? _entryTable.Items[cursorIndex] as SpecFileEntryViewModel
            : _entryTable.SelectedItem as SpecFileEntryViewModel;

        WeakReferenceMessenger.Default.Send(
            new FileTableSelectionChangedMessage(
                AssociatedObject.Identity,
                selectedItems,
                selectedRows.Any(static item => item.Model is null),
                activeItem));
    }

    private int? GetCurrentIndex() =>
        ClampIndex(_selectionCursorIndex)
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

        if (GetRowIndex(_entryTable.SelectedItem as SpecFileEntryViewModel) is { } selectedItemIndex)
        {
            return selectedItemIndex;
        }

        foreach (var item in _entryTable.SelectedItems.OfType<SpecFileEntryViewModel>().Reverse())
        {
            if (GetRowIndex(item) is { } selectedIndex)
            {
                return selectedIndex;
            }
        }

        return null;
    }

    private int? GetRowIndex(SpecFileEntryViewModel? item)
    {
        if (_entryTable is null || item is null)
        {
            return null;
        }

        var index = _entryTable.Items.IndexOf(item);
        return index >= 0 ? index : null;
    }

    private int? ClampIndex(int? index)
    {
        if (_entryTable is null || _entryTable.Items.Count == 0 || index is null)
        {
            return null;
        }

        return Math.Clamp(index.Value, 0, _entryTable.Items.Count - 1);
    }

    private void SyncNativeKeyboardState(int anchorIndex, int cursorIndex)
    {
        if (_entryTable is null)
        {
            return;
        }

        try
        {
            LastSelectionUnitProperty?.SetValue(_entryTable, TableViewSelectionUnit.Row);
            SelectionStartRowIndexProperty?.SetValue(_entryTable, (int?)anchorIndex);
            CurrentRowIndexProperty?.SetValue(_entryTable, (int?)cursorIndex);
            SelectionStartCellSlotProperty?.SetValue(_entryTable, null);
        }
        catch
        {
            // This is only compensating for WinUI.TableView 1.4.1 internals.
            // If the package changes those names, the explicit range selection
            // above still works and native follow-up navigation falls back.
        }
    }

    private bool EnsureTable()
    {
        if (AssociatedObject is null)
        {
            return false;
        }

        var table = AssociatedObject.Table;

        if (!ReferenceEquals(_entryTable, table))
        {
            DetachTableEvents();
            _entryTable = table;
        }

        AttachTableEvents();
        return true;
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

    private static bool IsModifierDown(VirtualKey key) =>
        InputKeyboardSource.GetKeyStateForCurrentThread(key)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

    private IReadOnlyList<SpecFileEntryViewModel> GetSelectedItemsSnapshot()
    {
        if (_entryTable is null)
        {
            return [];
        }

        return _entryTable.SelectedItems
            .OfType<SpecFileEntryViewModel>()
            .Where(static item => item.Model is not null)
            .ToList();
    }
}
