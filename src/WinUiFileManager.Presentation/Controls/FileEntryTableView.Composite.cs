using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using WinUI.TableView;
using WinUiFileManager.Presentation.ViewModels;

namespace WinUiFileManager.Presentation.Controls;

public sealed partial class FileEntryTableView
{
    private bool _syncingColumnLayout;
    private bool _headerWidthFrozen;
    private bool _headerDoubleTapHooked;
    private HorizontalAlignment _headerWidthFreezeRestoreAlignment = HorizontalAlignment.Stretch;

    private TableView FileTable => BodyTable;

    private TableViewTemplateColumn NameColumn => BodyNameColumn;

    private void CaptureColumnLayoutIntoHostCore()
    {
        if (GridViewModel.Host is not null)
        {
            GridViewModel.Host.ColumnLayout =
                FilePaneTableSortSync.CaptureColumnWidths(HeaderTable, GridViewModel.Host.ColumnLayout);
        }
    }

    private void FocusGridCore()
    {
        var host = GridViewModel.Host;
        if (host is null || !host.IsInteractive)
        {
            return;
        }

        host.CurrentItem ??= host.ParentEntry ?? host.Items.FirstOrDefault();
        SyncSelectionFromHostCore();
        FocusCurrentSelectionCore();
    }

    private void SelectAllRowsCore()
    {
        var host = GridViewModel.Host;
        if (host is null || !host.IsInteractive || host.Items.Count == 0)
        {
            return;
        }

        _syncingSelection = true;
        try
        {
            HeaderTable.SelectedItem = null;
            BodyTable.SelectAll();
        }
        finally
        {
            _syncingSelection = false;
        }

        host.CurrentItem ??= host.Items[0];
        host.UpdateSelectionFromControl(BodyTable.SelectedItems.OfType<FileEntryViewModel>());
        ClearCurrentCellSlot();
    }

    private void ClearRowSelectionCore()
    {
        if (GridViewModel.Host is null)
        {
            return;
        }

        _syncingSelection = true;
        try
        {
            BodyTable.SelectedItems.Clear();
            BodyTable.SelectedItem = null;
            HeaderTable.SelectedItem =
                GridViewModel.Host.CurrentItem?.EntryKind == FileEntryKind.Parent
                    ? GridViewModel.Host.ParentEntry
                    : null;
        }
        finally
        {
            _syncingSelection = false;
        }

        GridViewModel.Host.UpdateSelectionFromControl([]);
        ClearCurrentCellSlot();
    }

    private void ApplyColumnResizeFromOptionsCore()
    {
        HeaderTable.CanResizeColumns = FilePaneDisplayOptions.EnableColumnResize;
        BodyTable.CanResizeColumns = false;
    }

    private void FreezeCurrentWidthCore()
    {
        FreezeTable(HeaderTable, ref _headerWidthFrozen, ref _headerWidthFreezeRestoreAlignment);
        if (_isWidthFrozen)
        {
            return;
        }

        var currentWidth = BodyTable.ActualWidth;
        if (currentWidth <= 0d)
        {
            return;
        }

        _widthFreezeRestoreAlignment = BodyTable.HorizontalAlignment;
        BodyTable.Width = currentWidth;
        BodyTable.HorizontalAlignment = HorizontalAlignment.Left;
        _isWidthFrozen = true;
    }

    private void ReleaseFrozenWidthCore()
    {
        ReleaseTable(HeaderTable, ref _headerWidthFrozen, _headerWidthFreezeRestoreAlignment);
        if (!_isWidthFrozen && double.IsNaN(BodyTable.Width))
        {
            return;
        }

        BodyTable.Width = double.NaN;
        BodyTable.HorizontalAlignment = _widthFreezeRestoreAlignment;
        _isWidthFrozen = false;
    }

    private void FileEntryTableViewLoadedCore()
    {
        HeaderTable.RowHeight = 32;
        BodyTable.RowHeight = 32;
        ApplyColumnResizeFromOptionsCore();
        SyncHeaderSortDirectionsCore();
        SyncHeaderAndBodyColumnWidthsCore();

        if (_headerDoubleTapHooked)
        {
            return;
        }

        HeaderTable.AddHandler(
            UIElement.DoubleTappedEvent,
            new DoubleTappedEventHandler(OnHeaderTableDoubleTapped),
            handledEventsToo: true);
        _headerDoubleTapHooked = true;
    }

    private void OnSortStateChangedCore() =>
        DispatcherQueue.TryEnqueue(SyncHeaderSortDirectionsCore);

    private void OnHostPropertyChangedCore(System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FilePaneViewModel.ColumnLayout))
        {
            DispatcherQueue.TryEnqueue(SyncHeaderAndBodyColumnWidthsCore);
            return;
        }

        if (e.PropertyName is nameof(FilePaneViewModel.CurrentItem) or nameof(FilePaneViewModel.ParentEntry))
        {
            if (!_syncingSelection)
            {
                DispatcherQueue.TryEnqueue(SyncSelectionFromHostCore);
            }
        }
    }

    private void SyncSelectionFromHostCore()
    {
        var host = GridViewModel.Host;
        if (host is null)
        {
            return;
        }

        _syncingSelection = true;
        try
        {
            HeaderTable.SelectedItem =
                host.CurrentItem?.EntryKind == FileEntryKind.Parent ? host.ParentEntry : null;

            BodyTable.SelectedItems.Clear();
            foreach (var item in host.GetExplicitSelectedEntries())
            {
                BodyTable.SelectedItems.Add(item);
            }

            if (host.CurrentItem is { EntryKind: not FileEntryKind.Parent } currentItem)
            {
                if (!BodyTable.SelectedItems.Contains(currentItem))
                {
                    BodyTable.SelectedItems.Add(currentItem);
                }

                BodyTable.SelectedItem = currentItem;
                var rowIndex = host.Items.IndexOf(currentItem);
                if (rowIndex >= 0)
                {
                    BodyTable.ScrollRowIntoView(rowIndex);
                    SyncTableViewKeyboardAnchor(rowIndex);
                }
            }
            else
            {
                BodyTable.SelectedItem = null;
            }
        }
        finally
        {
            _syncingSelection = false;
        }

        ClearCurrentCellSlot();
    }

    private void SyncHeaderSortDirectionsCore()
    {
        if (GridViewModel.Host is not null)
        {
            FilePaneTableSortSync.SyncColumnSortDirections(HeaderTable, GridViewModel.Host);
        }
    }

    private void SyncHeaderAndBodyColumnWidthsCore()
    {
        var host = GridViewModel.Host;
        if (host is null)
        {
            return;
        }

        _syncingColumnLayout = true;
        try
        {
            FilePaneTableSortSync.SyncColumnWidths(HeaderTable, host.ColumnLayout);
            FilePaneTableSortSync.SyncColumnWidths(BodyTable, host.ColumnLayout);
        }
        finally
        {
            _syncingColumnLayout = false;
        }
    }

    private void FocusCurrentSelectionCore()
    {
        var host = GridViewModel.Host;
        if (host is null)
        {
            return;
        }

        if (host.CurrentItem?.EntryKind == FileEntryKind.Parent && host.ParentEntry is not null)
        {
            HeaderTable.Focus(FocusState.Programmatic);
            return;
        }

        if (host.CurrentItem is { EntryKind: not FileEntryKind.Parent } currentItem)
        {
            var rowIndex = host.Items.IndexOf(currentItem);
            if (rowIndex >= 0)
            {
                BodyTable.ScrollRowIntoView(rowIndex);
                SyncTableViewKeyboardAnchor(rowIndex);
                ClearCurrentCellSlot();
            }

            BodyTable.Focus(FocusState.Programmatic);
            return;
        }

        if (host.ParentEntry is not null)
        {
            host.CurrentItem = host.ParentEntry;
            SyncSelectionFromHostCore();
            HeaderTable.Focus(FocusState.Programmatic);
            return;
        }

        BodyTable.Focus(FocusState.Programmatic);
    }

    private void FreezeTable(TableView table, ref bool isFrozen, ref HorizontalAlignment restoreAlignment)
    {
        if (isFrozen)
        {
            return;
        }

        var width = table.ActualWidth;
        if (width <= 0d)
        {
            return;
        }

        restoreAlignment = table.HorizontalAlignment;
        table.Width = width;
        table.HorizontalAlignment = HorizontalAlignment.Left;
        isFrozen = true;
    }

    private static void ReleaseTable(TableView table, ref bool isFrozen, HorizontalAlignment restoreAlignment)
    {
        if (!isFrozen && double.IsNaN(table.Width))
        {
            return;
        }

        table.Width = double.NaN;
        table.HorizontalAlignment = restoreAlignment;
        isFrozen = false;
    }

    private void HeaderTable_Sorting(object sender, TableViewSortingEventArgs e)
    {
        e.Handled = true;
        GridViewModel.ApplySortFromSortMemberPath(e.Column.SortMemberPath);
        SyncHeaderSortDirectionsCore();
    }

    private void HeaderTable_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingSelection || GridViewModel.Host?.ParentEntry is not { } parentEntry || !GridViewModel.Host.IsInteractive)
        {
            return;
        }

        ActivationRequested?.Invoke();

        _syncingSelection = true;
        try
        {
            if (BodyTable.SelectedItems.Count <= 1)
            {
                BodyTable.SelectedItems.Clear();
                BodyTable.SelectedItem = null;
            }

            HeaderTable.SelectedItem = parentEntry;
            GridViewModel.Host.CurrentItem = parentEntry;
        }
        finally
        {
            _syncingSelection = false;
        }

        GridViewModel.Host.UpdateSelectionFromControl(BodyTable.SelectedItems.OfType<FileEntryViewModel>());
        ClearCurrentCellSlot();
        HeaderTable.Focus(FocusState.Programmatic);
    }

    private void HeaderTable_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        var host = GridViewModel.Host;
        if (host is null || !host.IsInteractive || IsTextInputSource(e.OriginalSource as DependencyObject))
        {
            return;
        }

        var ctrl = IsModifierDown(VirtualKey.Control);
        var shift = IsModifierDown(VirtualKey.Shift);

        switch (e.Key)
        {
            case VirtualKey.Enter when !ctrl:
                if (ActivateEntry(host.ParentEntry))
                {
                    e.Handled = true;
                }
                break;
            case VirtualKey.Down when !ctrl && !shift:
                if (host.Items.Count > 0)
                {
                    SelectBodyRow(0, true);
                    e.Handled = true;
                }
                break;
            case VirtualKey.End when !ctrl && !shift:
                if (host.Items.Count > 0)
                {
                    SelectBodyRow(host.Items.Count - 1, true);
                    e.Handled = true;
                }
                break;
            case VirtualKey.PageDown when !ctrl && !shift:
                if (host.Items.Count > 0)
                {
                    SelectBodyRow(Math.Min(GetVisibleBodyRowCount() - 1, host.Items.Count - 1), true);
                    e.Handled = true;
                }
                break;
            case VirtualKey.Home when !ctrl:
                e.Handled = true;
                break;
            case VirtualKey.Back when !ctrl && !shift:
                if (!string.IsNullOrEmpty(host.IncrementalSearchText))
                {
                    host.BackspaceIncrementalSearch();
                    SyncSelectionFromHostCore();
                    FocusCurrentSelectionCore();
                }
                else
                {
                    host.NavigateUpCommand.Execute(null);
                }

                e.Handled = true;
                break;
            case VirtualKey.PageUp when ctrl:
                host.NavigateUpCommand.Execute(null);
                e.Handled = true;
                break;
            default:
                if (!ctrl && !shift && HandleIncrementalSearch(host, e.Key))
                {
                    SyncSelectionFromHostCore();
                    FocusCurrentSelectionCore();
                    e.Handled = true;
                }
                break;
        }
    }

    private void HeaderTable_GotFocus(object sender, RoutedEventArgs e)
    {
        ActivationRequested?.Invoke();
        if (GridViewModel.Host?.CurrentItem?.EntryKind == FileEntryKind.Parent)
        {
            SyncSelectionFromHostCore();
        }
    }

    private void HeaderTable_LayoutUpdated(object sender, object e)
    {
        var host = GridViewModel.Host;
        if (host is null || _syncingColumnLayout)
        {
            return;
        }

        var captured = FilePaneTableSortSync.CaptureColumnWidths(HeaderTable, host.ColumnLayout);
        if (captured == host.ColumnLayout)
        {
            FilePaneTableSortSync.SyncColumnWidths(BodyTable, captured);
            return;
        }

        _syncingColumnLayout = true;
        try
        {
            FilePaneTableSortSync.SyncColumnWidths(BodyTable, captured);
            host.ColumnLayout = captured;
        }
        finally
        {
            _syncingColumnLayout = false;
        }
    }

    private void BodyTable_BeginningEdit(object sender, TableViewBeginningEditEventArgs e) =>
        FileTable_BeginningEdit(sender, e);

    private void BodyTable_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingSelection || GridViewModel.Host is not { } host || !host.IsInteractive)
        {
            return;
        }

        ActivationRequested?.Invoke();

        var entry =
            BodyTable.SelectedItem as FileEntryViewModel
            ?? e.AddedItems.OfType<FileEntryViewModel>().LastOrDefault()
            ?? BodyTable.SelectedItems.OfType<FileEntryViewModel>().LastOrDefault();

        _syncingSelection = true;
        try
        {
            HeaderTable.SelectedItem = null;
            if (entry is not null)
            {
                host.CurrentItem = entry;
                var rowIndex = host.Items.IndexOf(entry);
                if (rowIndex >= 0)
                {
                    SyncTableViewKeyboardAnchor(rowIndex);
                }
            }
        }
        finally
        {
            _syncingSelection = false;
        }

        host.UpdateSelectionFromControl(BodyTable.SelectedItems.OfType<FileEntryViewModel>());
        ClearCurrentCellSlot();
    }

    private void BodyTable_PreparingCellForEdit(object sender, TableViewPreparingCellForEditEventArgs e) =>
        FileTable_PreparingCellForEdit(sender, e);

    private void BodyTable_CellEditEnding(object sender, TableViewCellEditEndingEventArgs e) =>
        FileTable_CellEditEnding(sender, e);

    private void BodyTable_CellEditEnded(object sender, TableViewCellEditEndedEventArgs e)
    {
        FileTable_CellEditEnded(sender, e);
        ClearCurrentCellSlot();
        BodyTable.Focus(FocusState.Programmatic);
    }

    private void BodyTable_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        var host = GridViewModel.Host;
        if (host is null || !host.IsInteractive)
        {
            return;
        }

        var ctrl = IsModifierDown(VirtualKey.Control);
        var shift = IsModifierDown(VirtualKey.Shift);

        if (host.ActiveEditingEntry is not null
            && ((e.Key == VirtualKey.F2 && !ctrl && !shift)
                || (e.Key == VirtualKey.F6 && shift && !ctrl)))
        {
            if (!TryFocusNameEditor(host.ActiveEditingEntry))
            {
                BeginEditingNameCell(host.ActiveEditingEntry);
            }

            e.Handled = true;
            return;
        }

        if (IsTextInputSource(e.OriginalSource as DependencyObject))
        {
            return;
        }

        switch (e.Key)
        {
            case VirtualKey.Enter when !ctrl:
                if (ActivateEntry(BodyTable.SelectedItem as FileEntryViewModel))
                {
                    e.Handled = true;
                }
                break;
            case VirtualKey.Up when !ctrl && !shift:
                if (CanMoveToParent(host))
                {
                    SelectParentEntry(host);
                    e.Handled = true;
                }
                break;
            case VirtualKey.Home when !ctrl && !shift:
                if (host.ParentEntry is not null)
                {
                    SelectParentEntry(host);
                }
                else if (host.Items.Count > 0)
                {
                    SelectBodyRow(0, true);
                }

                e.Handled = true;
                break;
            case VirtualKey.End when !ctrl && !shift:
                if (host.Items.Count > 0)
                {
                    SelectBodyRow(host.Items.Count - 1, true);
                    e.Handled = true;
                }
                break;
            case VirtualKey.PageUp when !ctrl && !shift:
                MoveByPage(host, -1);
                e.Handled = true;
                break;
            case VirtualKey.PageDown when !ctrl && !shift:
                MoveByPage(host, 1);
                e.Handled = true;
                break;
        }
    }

    private void BodyTable_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        FileTable_KeyDown(sender, e);
        if (e.Handled)
        {
            ClearCurrentCellSlot();
        }
    }

    private void BodyTable_GotFocus(object sender, RoutedEventArgs e)
    {
        ActivationRequested?.Invoke();
        ClearCurrentCellSlot();
    }

    private void OnHeaderTableDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (ActivateEntry(GridViewModel.Host?.ParentEntry))
        {
            e.Handled = true;
        }
    }

    private void SelectParentEntry(FilePaneViewModel host)
    {
        if (host.ParentEntry is null)
        {
            return;
        }

        _syncingSelection = true;
        try
        {
            if (BodyTable.SelectedItems.Count <= 1)
            {
                BodyTable.SelectedItems.Clear();
                BodyTable.SelectedItem = null;
            }

            HeaderTable.SelectedItem = host.ParentEntry;
            host.CurrentItem = host.ParentEntry;
        }
        finally
        {
            _syncingSelection = false;
        }

        host.UpdateSelectionFromControl(BodyTable.SelectedItems.OfType<FileEntryViewModel>());
        ClearCurrentCellSlot();
        HeaderTable.Focus(FocusState.Programmatic);
    }

    private void SelectBodyRow(int index, bool clearSelection)
    {
        var host = GridViewModel.Host;
        if (host is null || index < 0 || index >= host.Items.Count)
        {
            return;
        }

        var entry = host.Items[index];
        _syncingSelection = true;
        try
        {
            HeaderTable.SelectedItem = null;
            if (clearSelection)
            {
                BodyTable.SelectedItems.Clear();
            }

            if (!BodyTable.SelectedItems.Contains(entry))
            {
                BodyTable.SelectedItems.Add(entry);
            }

            BodyTable.SelectedItem = entry;
            host.CurrentItem = entry;
        }
        finally
        {
            _syncingSelection = false;
        }

        host.UpdateSelectionFromControl(BodyTable.SelectedItems.OfType<FileEntryViewModel>());
        BodyTable.ScrollRowIntoView(index);
        SyncTableViewKeyboardAnchor(index);
        ClearCurrentCellSlot();
        BodyTable.Focus(FocusState.Programmatic);
    }

    private void MoveByPage(FilePaneViewModel host, int direction)
    {
        if (host.Items.Count == 0)
        {
            return;
        }

        var visibleCount = GetVisibleBodyRowCount();
        if (host.CurrentItem?.EntryKind == FileEntryKind.Parent)
        {
            if (direction > 0)
            {
                SelectBodyRow(Math.Min(visibleCount - 1, host.Items.Count - 1), true);
            }

            return;
        }

        var currentIndex = host.CurrentItem is { EntryKind: not FileEntryKind.Parent } currentItem
            ? host.Items.IndexOf(currentItem)
            : 0;
        if (currentIndex < 0)
        {
            currentIndex = 0;
        }

        var targetIndex = currentIndex + (direction * visibleCount);
        if (direction < 0 && targetIndex < 0 && host.ParentEntry is not null)
        {
            SelectParentEntry(host);
            return;
        }

        SelectBodyRow(Math.Clamp(targetIndex, 0, host.Items.Count - 1), true);
    }

    private bool CanMoveToParent(FilePaneViewModel host)
    {
        if (host.ParentEntry is null)
        {
            return false;
        }

        var current = BodyTable.SelectedItem as FileEntryViewModel ?? host.CurrentItem;
        return current is not null && host.Items.IndexOf(current) <= 0;
    }

    private int GetVisibleBodyRowCount() =>
        Math.Max(1, (int)(BodyTable.ActualHeight / Math.Max(BodyTable.RowHeight, 1d)) - 1);

    private static bool HandleIncrementalSearch(FilePaneViewModel host, VirtualKey key)
    {
        if (!IsTypingChar(key))
        {
            return false;
        }

        var c = VirtualKeyToChar(key);
        if (c == '\0')
        {
            return false;
        }

        host.HandleIncrementalSearch(c);
        return true;
    }
}
