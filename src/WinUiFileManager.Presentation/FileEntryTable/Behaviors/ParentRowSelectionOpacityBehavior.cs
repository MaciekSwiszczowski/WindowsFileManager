using WinUiFileManager.Presentation.FileEntryTable;

namespace WinUiFileManager.Presentation.FileEntryTable.Behaviors;

public sealed class ParentRowSelectionOpacityBehavior : FileEntryTableBehavior
{
    private const double ParentSelectionOpacity = 0.5d;
    private const double DefaultSelectionOpacity = 1d;

    private SpecFileEntryViewModel? _dimmedParentItem;
    private bool _isParentRowSelected;

    protected override void OnAttached()
    {
        base.OnAttached();
        WeakReferenceMessenger.Default.Register<FileTableSelectionChangedMessage>(this, OnFileTableSelectionChanged);
    }

    protected override void OnDetaching()
    {
        ResetParentSelectionOpacity();
        _dimmedParentItem = null;
        _isParentRowSelected = false;
        WeakReferenceMessenger.Default.Unregister<FileTableSelectionChangedMessage>(this);

        base.OnDetaching();
    }

    private void OnFileTableSelectionChanged(object recipient, FileTableSelectionChangedMessage message)
    {
        if (AssociatedObject is null || message.Identity != AssociatedObject.Identity)
        {
            return;
        }

        _isParentRowSelected = message.IsParentRowSelected;
        UpdateParentSelectionOpacity(queueRetry: true);
    }

    private void UpdateParentSelectionOpacity(bool queueRetry)
    {
        if (AssociatedObject is null)
        {
            return;
        }

        var table = AssociatedObject.Table;
        if (!_isParentRowSelected)
        {
            ResetParentSelectionOpacity(table);
            return;
        }

        if (FileEntryTableBehaviorHelper.GetParentItem(table) is not { } parentItem
            || !SpecFileEntryViewModel.IsParentEntry(parentItem))
        {
            ResetParentSelectionOpacity(table);
            return;
        }

        if (!ReferenceEquals(_dimmedParentItem, parentItem))
        {
            ResetParentSelectionOpacity(table);
        }

        SetItemSelectionOpacity(table, parentItem, ParentSelectionOpacity);
        _dimmedParentItem = parentItem;
        QueueParentSelectionOpacityRetry(queueRetry);
    }

    private void QueueParentSelectionOpacityRetry(bool queueRetry)
    {
        if (queueRetry)
        {
            AssociatedObject?.DispatcherQueue.TryEnqueue(() =>
            {
                UpdateParentSelectionOpacity(queueRetry: false);
            });
        }
    }

    private void ResetParentSelectionOpacity()
    {
        if (AssociatedObject is { } view)
        {
            ResetParentSelectionOpacity(view.Table);
        }
    }

    private void ResetParentSelectionOpacity(TableView table)
    {
        if (_dimmedParentItem is not { } dimmedParentItem)
        {
            return;
        }

        SetItemSelectionOpacity(table, dimmedParentItem, DefaultSelectionOpacity);
        _dimmedParentItem = null;
    }

    private static void SetItemSelectionOpacity(TableView table, SpecFileEntryViewModel item, double opacity)
    {
        if (table.ContainerFromItem(item) is TableViewRow row)
        {
            row.Opacity = opacity;
        }
    }

}
