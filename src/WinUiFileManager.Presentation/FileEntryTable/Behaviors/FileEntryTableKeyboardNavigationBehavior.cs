namespace WinUiFileManager.Presentation.FileEntryTable.Behaviors;

/// <summary>
/// Handles plain table keyboard navigation that WinUI.TableView does not provide
/// reliably for this control:
/// Home selects the first visible row,
/// End selects the last visible row,
/// PageUp selects the row one visible page above the current row,
/// PageDown selects the row one visible page below the current row.
/// Page movement clamps at the list boundaries and scrolls the target row into view.
/// </summary>
public sealed class FileEntryTableKeyboardNavigationBehavior : Behavior<SpecFileEntryTableView>
{
    private TableView? _entryTable;
    private bool _eventsAttached;

    protected override void OnAttached()
    {
        base.OnAttached();
        AssociatedObject.Loaded += OnLoaded;
    }

    protected override void OnDetaching()
    {
        if (AssociatedObject is { } view)
        {
            view.Loaded -= OnLoaded;
        }

        DetachTableEvents();
        _entryTable = null;

        base.OnDetaching();
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => EnsureTable();

    private void EntryTable_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Handled
            || IsModifierDown(VirtualKey.Shift)
            || IsModifierDown(VirtualKey.Control)
            || IsModifierDown(VirtualKey.Menu)
            || !EnsureTable()
            || _entryTable!.Items.Count == 0
            || !TryGetTargetIndex(e.Key, out var targetIndex))
        {
            return;
        }

        SelectSingleRow(targetIndex);
        e.Handled = true;
    }

    private bool TryGetTargetIndex(VirtualKey key, out int targetIndex)
    {
        var currentIndex = GetCurrentIndex();
        targetIndex = key switch
        {
            VirtualKey.Home => 0,
            VirtualKey.End => _entryTable!.Items.Count - 1,
            VirtualKey.PageUp => currentIndex - GetPageRowCount(),
            VirtualKey.PageDown => currentIndex + GetPageRowCount(),
            _ => currentIndex,
        };

        if (key is not (VirtualKey.Home or VirtualKey.End or VirtualKey.PageUp or VirtualKey.PageDown))
        {
            return false;
        }

        targetIndex = Math.Clamp(targetIndex, 0, _entryTable!.Items.Count - 1);
        return true;
    }

    private int GetCurrentIndex()
    {
        if (_entryTable is null)
        {
            return 0;
        }

        if (_entryTable.SelectedIndex >= 0)
        {
            return _entryTable.SelectedIndex;
        }

        if (_entryTable.SelectedItem is not null)
        {
            var selectedItemIndex = _entryTable.Items.IndexOf(_entryTable.SelectedItem);
            if (selectedItemIndex >= 0)
            {
                return selectedItemIndex;
            }
        }

        foreach (var item in _entryTable.SelectedItems.Reverse())
        {
            var selectedIndex = _entryTable.Items.IndexOf(item);
            if (selectedIndex >= 0)
            {
                return selectedIndex;
            }
        }

        return 0;
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

    private void SelectSingleRow(int targetIndex)
    {
        if (_entryTable?.Items[targetIndex] is not { } item)
        {
            return;
        }

        _entryTable.SelectedItems.Clear();
        _entryTable.SelectedItems.Add(item);
        _entryTable.ScrollRowIntoView(targetIndex);
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

    private void AttachTableEvents()
    {
        if (_eventsAttached || _entryTable is null)
        {
            return;
        }

        _entryTable.PreviewKeyDown += EntryTable_PreviewKeyDown;
        _eventsAttached = true;
    }

    private void DetachTableEvents()
    {
        if (!_eventsAttached)
        {
            return;
        }

        if (_entryTable is not null)
        {
            _entryTable.PreviewKeyDown -= EntryTable_PreviewKeyDown;
        }

        _eventsAttached = false;
    }

    private static bool IsModifierDown(VirtualKey key) =>
        InputKeyboardSource.GetKeyStateForCurrentThread(key)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
}
