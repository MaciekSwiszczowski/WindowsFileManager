namespace WinUiFileManager.Presentation.FileEntryTable.Behaviors;

/// <summary>
/// Handles plain table keyboard navigation that WinUI.TableView does not provide
/// reliably for this control:
/// Home selects the first visible row,
/// End selects the last visible row,
/// PageUp selects the first visible row when the current row is inside the viewport
/// and not already first; otherwise it moves up by the current visible row count,
/// PageDown selects the last visible row when the current row is inside the viewport
/// and not already last; otherwise it moves down by the current visible row count.
/// Page movement clamps at the list boundaries and scrolls the target row into view.
/// </summary>
public sealed class FileEntryTableKeyboardNavigationBehavior : FileEntryTableBehavior
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
            || FileEntryTableBehaviorHelper.HasAnyModifier(
                VirtualKey.Shift,
                VirtualKey.Control,
                VirtualKey.Menu)
            || !EnsureTable()
            || _entryTable!.Items.Count == 0
            || !FileEntryTableBehaviorHelper.TryGetNavigationTargetIndex(
                _entryTable,
                e.Key,
                FileEntryTableBehaviorHelper.GetCurrentSelectedIndex(_entryTable),
                out var targetIndex))
        {
            return;
        }

        FileEntryTableBehaviorHelper.SelectSingleRow(_entryTable, targetIndex);
        e.Handled = true;
    }

    private bool EnsureTable()
    {
        return FileEntryTableBehaviorHelper.EnsureTable(
            AssociatedObject,
            ref _entryTable,
            DetachTableEvents,
            AttachTableEvents);
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
}
