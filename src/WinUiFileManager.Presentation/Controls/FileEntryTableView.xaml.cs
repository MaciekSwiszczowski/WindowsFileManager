using System.Reflection;
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

public sealed partial class FileEntryTableView
{
    // WinUI.TableView keeps its arrow-key anchor in two internal members:
    //   - LastSelectionUnit (set to Row on mouse tap)
    //   - CurrentRowIndex   (set to the tapped row index)
    // Both are 'internal', but get stale when we change selection
    // programmatically (e.g. after navigating into/out of a folder). Reflecting
    // them lets us keep the control's native keyboard selection working from
    // the correct row without replacing any of the control's own logic.
    private static readonly PropertyInfo? LastSelectionUnitProperty =
        typeof(TableView).GetProperty(
            "LastSelectionUnit",
            BindingFlags.NonPublic | BindingFlags.Instance);

    private static readonly PropertyInfo? CurrentRowIndexProperty =
        typeof(TableView).GetProperty(
            "CurrentRowIndex",
            BindingFlags.NonPublic | BindingFlags.Instance);

    // SelectionStartRowIndex is the anchor that TableView.SelectRows uses for
    // Shift+Arrow range selection. SelectRows only initializes it via
    // `SelectionStartRowIndex ??= slot.Row;`, so when we change SelectedItem
    // programmatically (navigating into/out of a folder) the anchor keeps its
    // old value from the previous folder. The next Shift+Down then extends
    // from that stale anchor, selecting a large, unexpected range. Reset it
    // together with CurrentRowIndex so Shift+Arrow always anchors at the row
    // the user sees selected.
    private static readonly PropertyInfo? SelectionStartRowIndexProperty =
        typeof(TableView).GetProperty(
            "SelectionStartRowIndex",
            BindingFlags.NonPublic | BindingFlags.Instance);

    private static readonly PropertyInfo? SelectionStartCellSlotProperty =
        typeof(TableView).GetProperty(
            "SelectionStartCellSlot",
            BindingFlags.NonPublic | BindingFlags.Instance);

    private static readonly PropertyInfo? CurrentCellSlotProperty =
        typeof(TableView).GetProperty(
            nameof(TableView.CurrentCellSlot),
            BindingFlags.Public | BindingFlags.Instance);

    private static readonly MethodInfo? GetCellFromSlotMethod =
        typeof(TableView).GetMethod(
            "GetCellFromSlot",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

    private static readonly MethodInfo? BeginCellEditingMethod =
        typeof(TableViewCell).GetMethod(
            "BeginCellEditing",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

    private static readonly MethodInfo? EndCellEditingMethod =
        typeof(TableViewColumn).GetMethod(
            "EndCellEditing",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

    private bool _syncingSelection;
    private bool _endingNameCellEdit;
    private bool _isWidthFrozen;
    private HorizontalAlignment _widthFreezeRestoreAlignment = HorizontalAlignment.Stretch;
    private FilePaneViewModel? _currentItemSyncHost;

    public FileEntryTableView()
    {
        InitializeComponent();
        DataContext = GridViewModel;
        GridViewModel.SortStateChanged += OnSortStateChanged;

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
        {
            _currentItemSyncHost.PropertyChanged -= OnHostPropertyChanged;
            _currentItemSyncHost.RenameRequested -= OnHostRenameRequested;
        }

        GridViewModel.Attach(host);
        _currentItemSyncHost = host;

        if (host is not null)
        {
            host.PropertyChanged += OnHostPropertyChanged;
            host.RenameRequested += OnHostRenameRequested;
        }

        DispatcherQueue.TryEnqueue(() =>
        {
            SyncHeaderSortDirectionsCore();
            SyncHeaderAndBodyColumnWidthsCore();
            SyncSelectionFromHostCore();
        });
    }

    public void CaptureColumnLayoutIntoHost()
        => CaptureColumnLayoutIntoHostCore();

    public void FocusGrid()
        => FocusGridCore();

    public void SelectAllRows()
        => SelectAllRowsCore();

    public void ClearRowSelection()
        => ClearRowSelectionCore();

    public void ApplyColumnResizeFromOptions()
        => ApplyColumnResizeFromOptionsCore();

    public void FreezeCurrentWidth()
        => FreezeCurrentWidthCore();

    public void ReleaseFrozenWidth()
        => ReleaseFrozenWidthCore();

    private void FileEntryTableView_Loaded(object sender, RoutedEventArgs e)
        => FileEntryTableViewLoadedCore();

    private void OnSortStateChanged(object? sender, EventArgs e)
        => OnSortStateChangedCore();

    private void OnHostPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        => OnHostPropertyChangedCore(e);

    private void SyncTableViewKeyboardAnchor(int rowIndex)
    {
        try
        {
            LastSelectionUnitProperty?.SetValue(FileTable, TableViewSelectionUnit.Row);
            CurrentRowIndexProperty?.SetValue(FileTable, (int?)rowIndex);
            SelectionStartRowIndexProperty?.SetValue(FileTable, (int?)rowIndex);
            SelectionStartCellSlotProperty?.SetValue(FileTable, null);
        }
        catch
        {
            // If WinUI.TableView ever renames these internals we silently
            // fall back to the control's default keyboard anchor behavior.
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
            {
                return;
            }

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

    private void FileTable_BeginningEdit(object sender, TableViewBeginningEditEventArgs e)
    {
        if (!ReferenceEquals(e.Column, NameColumn))
        {
            e.Cancel = true;
            return;
        }

        if (e.DataItem is not FileEntryViewModel entry
            || !ReferenceEquals(GridViewModel.Host?.ActiveEditingEntry, entry))
        {
            e.Cancel = true;
        }
    }

    private void FileTable_Sorting(object sender, TableViewSortingEventArgs e)
    {
        e.Handled = true; // Prevent TableView from sorting internally
        GridViewModel.ApplySortFromSortMemberPath(e.Column.SortMemberPath);
        if (GridViewModel.Host is not null)
        {
            FilePaneTableSortSync.SyncColumnSortDirections(FileTable, GridViewModel.Host);
        }
    }

    private void FileTable_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingSelection)
        {
            return;
        }

        var host = GridViewModel.Host;
        if (host is null || !host.IsInteractive)
        {
            return;
        }

        ActivationRequested?.Invoke();

        _syncingSelection = true;
        try
        {
            if (FileTable.SelectedItem is FileEntryViewModel entry)
            {
                host.CurrentItem = entry;

                var rowIndex = host.Items.IndexOf(entry);
                if (rowIndex >= 0)
                {
                    SyncTableViewKeyboardAnchor(rowIndex);
                    ClearCurrentCellSlot();
                }
            }
        }
        finally
        {
            _syncingSelection = false;
        }

        host.UpdateSelectionFromControl(FileTable.SelectedItems.OfType<FileEntryViewModel>());
        MoveFocusToCurrentItem();
    }

    private void OnFileTableDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        var entry = FindEntryInVisualTree(e.OriginalSource as DependencyObject)
            ?? FileTable.SelectedItem as FileEntryViewModel;

        if (ActivateEntry(entry))
        {
            e.Handled = true;
        }
    }

    private static FileEntryViewModel? FindEntryInVisualTree(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is FrameworkElement { DataContext: FileEntryViewModel entry })
            {
                return entry;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return null;
    }

    private bool ActivateEntry(FileEntryViewModel? entry)
    {
        var host = GridViewModel.Host;
        if (host is null || !host.IsInteractive || entry is null)
        {
            return false;
        }

        host.CurrentItem = entry;

        if (host.NavigateIntoCommand.CanExecute(null))
        {
            host.NavigateIntoCommand.Execute(null);
        }

        return true;
    }

    private void FileTable_GotFocus(object sender, RoutedEventArgs e) =>
        ActivationRequested?.Invoke();

    private void OnHostRenameRequested(object? sender, FileEntryViewModel entry)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (!TryFocusNameEditor(entry))
            {
                BeginEditingNameCell(entry);
            }
        });
    }

    private void BeginEditingNameCell(FileEntryViewModel entry, int retries = 5)
    {
        var host = GridViewModel.Host;
        if (host is null)
        {
            return;
        }

        var rowIndex = host.Items.IndexOf(entry);
        if (rowIndex < 0)
        {
            return;
        }

        var slot = new TableViewCellSlot(rowIndex, FileTable.Columns.IndexOf(NameColumn));

        _syncingSelection = true;
        try
        {
            FileTable.SelectedItem = entry;
            FileTable.CurrentCellSlot = slot;
            FileTable.ScrollRowIntoView(rowIndex);
        }
        finally
        {
            _syncingSelection = false;
        }

        SyncTableViewKeyboardAnchor(rowIndex);
        host.UpdateSelectionFromControl(FileTable.SelectedItems.OfType<FileEntryViewModel>());

        if (TryGetCellFromSlot(slot) is not { } cell)
        {
            if (retries > 0)
            {
                DispatcherQueue.TryEnqueue(() => BeginEditingNameCell(entry, retries - 1));
            }

            return;
        }

        _ = TryBeginCellEditingAsync(cell);
    }

    private void FileTable_PreparingCellForEdit(object sender, TableViewPreparingCellForEditEventArgs e)
    {
        if (!ReferenceEquals(e.Column, NameColumn))
        {
            return;
        }

        if (e.DataItem is FileEntryViewModel entry
            && FindDescendant<TextBox>(e.EditingElement) is { } nameEditor)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                nameEditor.Focus(FocusState.Keyboard);
                SelectNameStem(nameEditor, entry.Name);
            });
        }
    }

    private async void FileTable_CellEditEnding(object sender, TableViewCellEditEndingEventArgs e)
    {
        if (!ReferenceEquals(e.Column, NameColumn))
        {
            return;
        }

        if (e.DataItem is not FileEntryViewModel entry || GridViewModel.Host is not { } host)
        {
            return;
        }

        if (_endingNameCellEdit)
        {
            return;
        }

        if (FindDescendant<TextBox>(e.EditingElement) is { } textBox)
        {
            host.EditBuffer = textBox.Text;
        }

        if (e.EditAction == TableViewEditAction.Cancel)
        {
            host.CancelRename(entry);
            return;
        }

        if (!host.IsEditing)
        {
            return;
        }

        e.Cancel = true;

        var commitSucceeded = await host.CommitRenameAsync(entry, host.EditBuffer, CancellationToken.None);
        if (commitSucceeded)
        {
            CompleteNameCellEdit(e.Cell, entry, TableViewEditAction.Commit, entry.Name);
            return;
        }

        DispatcherQueue.TryEnqueue(() => RefocusNameEditor(e.Cell));
    }

    private void FileTable_CellEditEnded(object sender, TableViewCellEditEndedEventArgs e)
    {
        if (!ReferenceEquals(e.Column, NameColumn))
        {
        }
    }
    // PreviewKeyDown fires before the TableView's internal handling.
    // Enter is handled here so the TableView cannot consume it first.
    // Arrow / Home / End / Shift+Arrow etc. are left to the control's own
    // keyboard selection logic so mouse and keyboard multi-selection stay
    // consistent; SyncTableViewKeyboardAnchor keeps its internal anchor
    // correct after programmatic navigation.
    private void OnPreviewKeyDown(object sender, KeyRoutedEventArgs e)
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
                if (FileTable.SelectedItem is FileEntryViewModel entry)
                {
                    host.CurrentItem = entry;
                    host.NavigateIntoCommand.Execute(null);
                    e.Handled = true;
                }
                break;

            case VirtualKey.Home when !ctrl:
                MoveSelectionToBoundary(moveToLast: false);
                e.Handled = true;
                break;

            case VirtualKey.End when !ctrl:
                MoveSelectionToBoundary(moveToLast: true);
                e.Handled = true;
                break;

            case VirtualKey.PageUp when !ctrl:
                MoveSelectionByPage(-1);
                e.Handled = true;
                break;

            case VirtualKey.PageDown when !ctrl:
                MoveSelectionByPage(1);
                e.Handled = true;
                break;
        }
    }

    private void FileTable_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        var host = GridViewModel.Host;
        if (host is null || !host.IsInteractive)
        {
            return;
        }

        if (IsTextInputSource(e.OriginalSource as DependencyObject))
        {
            return;
        }

        var ctrl = IsModifierDown(VirtualKey.Control);
        var shift = IsModifierDown(VirtualKey.Shift);

        switch (e.Key)
        {
            // Enter is handled in PreviewKeyDown to beat the TableView's internal routing.

            case VirtualKey.A when ctrl && !shift:
                SelectAllRows();
                e.Handled = true;
                break;

            case VirtualKey.A when ctrl && shift:
                ClearRowSelection();
                e.Handled = true;
                break;

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
                    ToggleSelection(spaceSelected);
                    e.Handled = true;
                }
                break;

            // Ctrl+Space: toggle selection without moving cursor
            case VirtualKey.Space when ctrl:
                if (FileTable.SelectedItem is FileEntryViewModel ctrlSpaceSelected)
                {
                    ToggleSelection(ctrlSpaceSelected);
                    e.Handled = true;
                }
                break;

            // Insert: toggle selection AND advance cursor one row (spec §6.2 and §12.15)
            case VirtualKey.Insert:
                if (FileTable.SelectedItem is FileEntryViewModel insertSelected)
                {
                    var insertIdx = host.Items.IndexOf(insertSelected);
                    ToggleSelection(insertSelected);
                    var nextIdx = Math.Min(insertIdx + 1, host.Items.Count - 1);
                    if (nextIdx >= 0)
                    {
                        _syncingSelection = true;
                        try
                        {
                            FileTable.SelectedItem = host.Items[nextIdx];
                        }
                        finally
                        {
                            _syncingSelection = false;
                        }

                        SyncTableViewKeyboardAnchor(nextIdx);
                    }

                    e.Handled = true;
                }
                break;

            case VirtualKey.Escape:
                if (host.SelectedCount > 0)
                {
                    ClearRowSelection();
                }
                else
                {
                    host.ClearIncrementalSearch();
                }
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

    public void SyncSelectionFromHost()
        => SyncSelectionFromHostCore();

    private void ToggleSelection(FileEntryViewModel item)
    {
        if (item.EntryKind == FileEntryKind.Parent)
        {
            return;
        }

        _syncingSelection = true;
        try
        {
            if (FileTable.SelectedItems.Contains(item))
            {
                FileTable.SelectedItems.Remove(item);
            }
            else
            {
                FileTable.SelectedItems.Add(item);
            }
        }
        finally
        {
            _syncingSelection = false;
        }

        GridViewModel.Host?.UpdateSelectionFromControl(FileTable.SelectedItems.OfType<FileEntryViewModel>());
    }

    private static bool IsTypingChar(VirtualKey key) =>
        key is >= VirtualKey.A and <= VirtualKey.Z
            or >= VirtualKey.Number0 and <= VirtualKey.Number9;

    private TableViewCell? TryGetCellFromSlot(TableViewCellSlot slot)
    {
        try
        {
            return GetCellFromSlotMethod?.Invoke(FileTable, [slot]) as TableViewCell;
        }
        catch
        {
            return null;
        }
    }

    private static async Task TryBeginCellEditingAsync(TableViewCell cell)
    {
        try
        {
            var result = BeginCellEditingMethod?.Invoke(cell, [new RoutedEventArgs()]);
            switch (result)
            {
                case Task<bool> boolTask:
                    await boolTask.ConfigureAwait(true);
                    break;
                case Task task:
                    await task.ConfigureAwait(true);
                    break;
            }
        }
        catch
        {
            // If the library changes its internal editing entry point, rename
            // simply won't enter edit mode until this helper is updated.
        }
    }

    private void CompleteNameCellEdit(
        TableViewCell cell,
        FileEntryViewModel entry,
        TableViewEditAction editAction,
        object uneditedValue)
    {
        if (EndCellEditingMethod is null)
        {
            return;
        }

        try
        {
            _endingNameCellEdit = true;
            EndCellEditingMethod.Invoke(NameColumn, [cell, entry, editAction, uneditedValue]);
        }
        catch
        {
            // If the package changes its edit completion hook, manual
            // verification will catch the stuck edit session.
        }
        finally
        {
            _endingNameCellEdit = false;
        }
    }

    private static T? FindDescendant<T>(DependencyObject? source)
        where T : DependencyObject
    {
        if (source is T match)
        {
            return match;
        }

        if (source is null)
        {
            return null;
        }

        var childCount = VisualTreeHelper.GetChildrenCount(source);
        for (var i = 0; i < childCount; i++)
        {
            if (FindDescendant<T>(VisualTreeHelper.GetChild(source, i)) is { } descendant)
            {
                return descendant;
            }
        }

        return null;
    }

    private static bool IsTextInputSource(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is TextBox or PasswordBox or RichEditBox or AutoSuggestBox)
            {
                return true;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return false;
    }

    private void RefocusNameEditor(TableViewCell cell)
    {
        if (FindDescendant<TextBox>(cell) is not { } nameEditor)
        {
            return;
        }

        nameEditor.Focus(FocusState.Keyboard);
        nameEditor.Select(nameEditor.Text.Length, 0);
    }

    private bool TryFocusNameEditor(FileEntryViewModel entry)
    {
        var host = GridViewModel.Host;
        if (host is null)
        {
            return false;
        }

        var rowIndex = host.Items.IndexOf(entry);
        if (rowIndex < 0)
        {
            return false;
        }

        var slot = new TableViewCellSlot(rowIndex, FileTable.Columns.IndexOf(NameColumn));

        _syncingSelection = true;
        try
        {
            FileTable.SelectedItem = entry;
            FileTable.CurrentCellSlot = slot;
            FileTable.ScrollRowIntoView(rowIndex);
        }
        finally
        {
            _syncingSelection = false;
        }

        SyncTableViewKeyboardAnchor(rowIndex);
        host.UpdateSelectionFromControl(FileTable.SelectedItems.OfType<FileEntryViewModel>());

        if (TryGetCellFromSlot(slot) is not { } cell
            || FindDescendant<TextBox>(cell) is not { } nameEditor)
        {
            return false;
        }

        nameEditor.Focus(FocusState.Keyboard);
        SelectNameStem(nameEditor, entry.Name);
        return true;
    }

    private static void SelectNameStem(TextBox nameEditor, string name)
    {
        var extension = Path.GetExtension(name);
        var stemLength = name.Length;

        if (!string.IsNullOrEmpty(extension)
            && !string.Equals(name, extension, StringComparison.OrdinalIgnoreCase)
            && extension.Length < name.Length)
        {
            stemLength = name.Length - extension.Length;
        }

        nameEditor.Select(0, Math.Max(stemLength, 0));
    }

    private void MoveSelectionToBoundary(bool moveToLast)
    {
        var host = GridViewModel.Host;
        if (host is null || host.Items.Count == 0)
        {
            return;
        }

        MoveSelectionToIndex(moveToLast ? host.Items.Count - 1 : 0);
    }

    private void MoveSelectionByPage(int direction)
    {
        var host = GridViewModel.Host;
        if (host is null || host.Items.Count == 0)
        {
            return;
        }

        var currentIndex = FileTable.SelectedItem is FileEntryViewModel selected
            ? host.Items.IndexOf(selected)
            : host.CurrentItem is not null
                ? host.Items.IndexOf(host.CurrentItem)
                : 0;

        if (currentIndex < 0)
        {
            currentIndex = 0;
        }

        var visibleRowCount = Math.Max(1, (int)(FileTable.ActualHeight / Math.Max(FileTable.RowHeight, 1d)) - 1);
        var targetIndex = Math.Clamp(currentIndex + (direction * visibleRowCount), 0, host.Items.Count - 1);
        MoveSelectionToIndex(targetIndex);
    }

    private void MoveSelectionToIndex(int index)
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
            FileTable.SelectedItems.Clear();
            FileTable.SelectedItem = entry;
            host.CurrentItem = entry;
        }
        finally
        {
            _syncingSelection = false;
        }

        host.UpdateSelectionFromControl(FileTable.SelectedItems.OfType<FileEntryViewModel>());
        FileTable.ScrollRowIntoView(index);
        SyncTableViewKeyboardAnchor(index);
        ClearCurrentCellSlot();
        MoveFocusToCurrentItem();
    }

    private void ClearCurrentCellSlot()
    {
        try
        {
            CurrentCellSlotProperty?.SetValue(FileTable, null);
        }
        catch
        {
            // If the control ever changes how it exposes the current cell,
            // we keep row selection behavior and simply lose the explicit
            // cell-focus clearing until this helper is updated.
        }
    }

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
