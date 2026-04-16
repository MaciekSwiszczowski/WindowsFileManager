using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;
using Windows.UI.Core;
using WinUI.TableView;
using WinUiFileManager.Presentation.ViewModels;

namespace WinUiFileManager.Presentation.Controls;

public sealed partial class FileEntryTableView : UserControl
{
    private bool _syncingSelection;
    private FilePaneViewModel? _currentItemSyncHost;

    public FileEntryTableView()
    {
        InitializeComponent();
        DataContext = GridViewModel;
        GridViewModel.SortStateChanged += OnSortStateChanged;
        // PreviewKeyDown fires before the TableView's internal key handling,
        // ensuring Enter on folders and '..' is not consumed by the control first.
        PreviewKeyDown += OnPreviewKeyDown;

        // WinUI.TableView's TableViewCell marks DoubleTapped as Handled when
        // IsReadOnly=True (see TableViewCell.OnDoubleTapped). That prevents the
        // event from bubbling to TableViewRow, so RowDoubleTapped never fires
        // and a plain XAML DoubleTapped subscription is also skipped. Subscribe
        // with handledEventsToo: true so we still receive the event.
        FileTable.AddHandler(
            UIElement.DoubleTappedEvent,
            new DoubleTappedEventHandler(OnFileTableDoubleTapped),
            handledEventsToo: true);
    }

    public FileEntryTableViewModel GridViewModel { get; } = new();

    public event Action? ActivationRequested;

    public void Attach(FilePaneViewModel? host)
    {
        if (_currentItemSyncHost is not null)
            _currentItemSyncHost.PropertyChanged -= OnHostPropertyChanged;

        GridViewModel.Attach(host);
        _currentItemSyncHost = host;

        if (host is not null)
            host.PropertyChanged += OnHostPropertyChanged;

        DispatcherQueue.TryEnqueue(() =>
        {
            if (GridViewModel.Host is not null)
                FilePaneTableSortSync.SyncColumnSortDirections(FileTable, GridViewModel.Host);
        });
    }

    public void FocusGrid()
    {
        FileTable.Focus(FocusState.Programmatic);
        if (GridViewModel.Host?.Items.Count > 0 && FileTable.SelectedItem is null)
            FileTable.SelectedItem = GridViewModel.Host.Items[0];
    }

    public void ApplyColumnResizeFromOptions()
    {
        FileTable.CanResizeColumns = FilePaneDisplayOptions.EnableColumnResize;
    }

    private void FileEntryTableView_Loaded(object sender, RoutedEventArgs e)
    {
        FileTable.RowHeight = 32;
        ApplyColumnResizeFromOptions();
        if (GridViewModel.Host is not null)
            FilePaneTableSortSync.SyncColumnSortDirections(FileTable, GridViewModel.Host);
    }

    private void OnSortStateChanged(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (GridViewModel.Host is not null)
                FilePaneTableSortSync.SyncColumnSortDirections(FileTable, GridViewModel.Host);
        });
    }

    private void OnHostPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(FilePaneViewModel.CurrentItem))
            return;

        var host = GridViewModel.Host;
        if (host is null || _syncingSelection)
            return;

        var current = host.CurrentItem;
        if (current is not null && !Equals(FileTable.SelectedItem, current))
        {
            _syncingSelection = true;
            try
            {
                FileTable.SelectedItem = current;
                var idx = host.Items.IndexOf(current);
                if (idx >= 0)
                    FileTable.ScrollRowIntoView(idx);
            }
            finally
            {
                _syncingSelection = false;
            }

            // Move the TableView's focused row (the one that shows the
            // accent border) to match CurrentItem. Without this the frame
            // lags behind after programmatic navigation, e.g. after going
            // up one directory.
            MoveFocusToCurrentItem();
        }
    }

    private void MoveFocusToCurrentItem(int retries = 5)
    {
        // The row container may not be realized yet right after PopulateItems
        // (virtualizer hasn't had a layout pass). Retry a few dispatcher ticks
        // so the container exists before we focus it. FocusState.Keyboard is
        // used so the system focus visual (our accent border) actually renders.
        DispatcherQueue.TryEnqueue(() =>
        {
            if (FileTable.SelectedItem is null)
                return;

            if (FileTable.ContainerFromItem(FileTable.SelectedItem) is Control container)
            {
                container.Focus(FocusState.Keyboard);
            }
            else if (retries > 0)
            {
                MoveFocusToCurrentItem(retries - 1);
            }
            else
            {
                FileTable.Focus(FocusState.Keyboard);
            }
        });
    }

    private void FileTable_Sorting(object sender, TableViewSortingEventArgs e)
    {
        e.Handled = true; // Prevent TableView from sorting internally
        GridViewModel.ApplySortFromSortMemberPath(e.Column?.SortMemberPath);
        if (GridViewModel.Host is not null)
            FilePaneTableSortSync.SyncColumnSortDirections(FileTable, GridViewModel.Host);
    }

    private void FileTable_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingSelection)
            return;

        ActivationRequested?.Invoke();

        var host = GridViewModel.Host;
        if (host is null)
            return;

        _syncingSelection = true;
        try
        {
            foreach (var added in e.AddedItems.OfType<FileEntryViewModel>())
                added.IsSelected = true;
            foreach (var removed in e.RemovedItems.OfType<FileEntryViewModel>())
                removed.IsSelected = false;

            if (FileTable.SelectedItem is FileEntryViewModel entry)
                host.CurrentItem = entry;

            host.NotifySelectionChanged();
        }
        finally
        {
            _syncingSelection = false;
        }
    }

    private void OnFileTableDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        var entry = FindEntryInVisualTree(e.OriginalSource as DependencyObject)
            ?? FileTable.SelectedItem as FileEntryViewModel;

        if (ActivateEntry(entry))
            e.Handled = true;
    }

    private static FileEntryViewModel? FindEntryInVisualTree(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is FrameworkElement fe && fe.DataContext is FileEntryViewModel entry)
                return entry;

            source = VisualTreeHelper.GetParent(source);
        }

        return null;
    }

    private bool ActivateEntry(FileEntryViewModel? entry)
    {
        var host = GridViewModel.Host;
        if (host is null || entry is null)
            return false;

        host.CurrentItem = entry;

        if (host.NavigateIntoCommand.CanExecute(null))
            host.NavigateIntoCommand.Execute(null);

        return true;
    }

    private void FileTable_GotFocus(object sender, RoutedEventArgs e) =>
        ActivationRequested?.Invoke();

    // PreviewKeyDown fires before the TableView's internal handling.
    // Enter is handled here so the TableView cannot consume it first.
    //
    // Plain Up/Down are also handled here because TableView's internal
    // keyboard navigation uses its private CurrentRowIndex cursor, which
    // is NOT updated by programmatic SelectedItem changes. After navigating
    // into/up a directory, that internal cursor would still point at the
    // row index from the previous folder, causing arrow-down to jump to a
    // seemingly random position. Driving navigation from our VM's Items
    // collection removes that desync entirely.
    //
    // Shift/Ctrl + arrow combinations are intentionally left to the
    // TableView's native handler so range-select behavior is preserved.
    private void OnPreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        var host = GridViewModel.Host;
        if (host is null)
            return;

        var ctrl = IsModifierDown(VirtualKey.Control);
        var shift = IsModifierDown(VirtualKey.Shift);

        switch (e.Key)
        {
            case VirtualKey.Enter when !ctrl:
                if (FileTable.SelectedItem is FileEntryViewModel entry)
                {
                    host.CurrentItem = entry;
                    host.NavigateIntoCommand.Execute(null);
                    e.Handled = true;
                }
                break;

            case VirtualKey.Down when !ctrl && !shift:
                if (MoveCurrentItemBy(1))
                    e.Handled = true;
                break;

            case VirtualKey.Up when !ctrl && !shift:
                if (MoveCurrentItemBy(-1))
                    e.Handled = true;
                break;
        }
    }

    private bool MoveCurrentItemBy(int delta)
    {
        var host = GridViewModel.Host;
        if (host is null || host.Items.Count == 0)
            return false;

        var anchor = FileTable.SelectedItem as FileEntryViewModel ?? host.CurrentItem;
        var currentIdx = anchor is null ? -1 : host.Items.IndexOf(anchor);
        var targetIdx = currentIdx < 0
            ? (delta > 0 ? 0 : host.Items.Count - 1)
            : Math.Clamp(currentIdx + delta, 0, host.Items.Count - 1);

        return MoveCurrentItemToIndex(targetIdx);
    }

    private bool MoveCurrentItemToIndex(int targetIdx)
    {
        var host = GridViewModel.Host;
        if (host is null || host.Items.Count == 0 || targetIdx < 0 || targetIdx >= host.Items.Count)
            return false;

        var target = host.Items[targetIdx];

        _syncingSelection = true;
        try
        {
            FileTable.SelectedItem = target;
            host.CurrentItem = target;
            FileTable.ScrollRowIntoView(targetIdx);
        }
        finally
        {
            _syncingSelection = false;
        }

        host.NotifySelectionChanged();
        MoveFocusToCurrentItem();
        return true;
    }

    private void FileTable_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        var host = GridViewModel.Host;
        if (host is null)
            return;

        var ctrl = IsModifierDown(VirtualKey.Control);

        switch (e.Key)
        {
            // Enter is handled in PreviewKeyDown to beat the TableView's internal routing.

            case VirtualKey.Back:
                if (!string.IsNullOrEmpty(host.IncrementalSearchText))
                {
                    host.BackspaceIncrementalSearch();
                    e.Handled = true;
                    break;
                }

                host.NavigateUpCommand.Execute(null);
                e.Handled = true;
                break;

            // PageUp with Ctrl = navigate to parent directory (Total Commander convention)
            case VirtualKey.PageUp when ctrl:
                host.NavigateUpCommand.Execute(null);
                e.Handled = true;
                break;

            // Space: toggle selection without moving cursor (spec §6.2 and §12.15)
            case VirtualKey.Space when !ctrl:
                if (FileTable.SelectedItem is FileEntryViewModel spaceSelected)
                {
                    host.ToggleSelection(spaceSelected);
                    SyncSelectionFromHost();
                    e.Handled = true;
                }
                break;

            // Ctrl+Space: toggle selection without moving cursor
            case VirtualKey.Space when ctrl:
                if (FileTable.SelectedItem is FileEntryViewModel ctrlSpaceSelected)
                {
                    host.ToggleSelection(ctrlSpaceSelected);
                    SyncSelectionFromHost();
                    e.Handled = true;
                }
                break;

            // Insert: toggle selection AND advance cursor one row (spec §6.2 and §12.15)
            case VirtualKey.Insert:
                if (FileTable.SelectedItem is FileEntryViewModel insertSelected)
                {
                    // Capture index before SyncSelectionFromHost clears SelectedItems
                    var insertIdx = host.Items.IndexOf(insertSelected);
                    host.ToggleSelection(insertSelected);
                    SyncSelectionFromHost();
                    // Advance cursor to next row (stay on last row if already there)
                    var nextIdx = Math.Min(insertIdx + 1, host.Items.Count - 1);
                    if (nextIdx >= 0)
                        FileTable.SelectedItem = host.Items[nextIdx];
                    e.Handled = true;
                }
                break;

            case VirtualKey.Escape:
                if (host.SelectedCount > 0)
                    host.ClearSelectionCommand.Execute(null);
                else
                    host.ClearIncrementalSearch();
                e.Handled = true;
                break;

            case VirtualKey.Home:
                MoveCurrentItemToIndex(0);
                e.Handled = true;
                break;

            case VirtualKey.End:
                MoveCurrentItemToIndex(host.Items.Count - 1);
                e.Handled = true;
                break;

            default:
                if (!ctrl && IsTypingChar(e.Key))
                {
                    var c = VirtualKeyToChar(e.Key);
                    if (c != '\0')
                    {
                        host.HandleIncrementalSearch(c);
                        e.Handled = true;
                    }
                }
                break;
        }
    }

    private void SelectItemAtOffsetFromCurrent(int delta)
    {
        var host = GridViewModel.Host;
        if (host is null || FileTable.SelectedItem is not FileEntryViewModel current)
            return;

        var idx = host.Items.IndexOf(current);
        if (idx < 0)
            return;

        var next = idx + delta;
        if (next >= 0 && next < host.Items.Count)
            FileTable.SelectedItem = host.Items[next];
    }

    private void SyncSelectionFromHost()
    {
        var host = GridViewModel.Host;
        if (host is null)
            return;

        _syncingSelection = true;
        try
        {
            FileTable.SelectedItems.Clear();
            foreach (var item in host.Items.Where(i => i.IsSelected))
                FileTable.SelectedItems.Add(item);
        }
        finally
        {
            _syncingSelection = false;
        }
    }

    private static bool IsTypingChar(VirtualKey key) =>
        key is >= VirtualKey.A and <= VirtualKey.Z
            or >= VirtualKey.Number0 and <= VirtualKey.Number9;

    private static char VirtualKeyToChar(VirtualKey key) => key switch
    {
        >= VirtualKey.A and <= VirtualKey.Z => (char)('A' + (key - VirtualKey.A)),
        >= VirtualKey.Number0 and <= VirtualKey.Number9 => (char)('0' + (key - VirtualKey.Number0)),
        _ => '\0',
    };

    private static bool IsModifierDown(VirtualKey key) =>
        InputKeyboardSource.GetKeyStateForCurrentThread(key)
            .HasFlag(CoreVirtualKeyStates.Down);
}
