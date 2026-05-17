namespace WinUiFileManager.Presentation.FileEntryTable.Behaviors;

public sealed class ParentRowSelectionOpacityBehavior : FileEntryTableBehaviorBase
{
    private const double ParentSelectionOpacity = 0.5d;
    private const double DefaultSelectionOpacity = 1d;

    private SpecFileEntryViewModel? _dimmedParentItem;
    private bool _isParentRowSelected;

    protected override void OnLoaded(FileEntryTableBehaviorContext context) =>
        context.Messenger.Register<FileTableSelectionChangedMessage>(this, OnFileTableSelectionChanged);

    protected override void OnUnloaded(FileEntryTableBehaviorContext context)
    {
        ResetParentSelectionOpacity(context.Table);
        _dimmedParentItem = null;
        _isParentRowSelected = false;
    }

    private void OnFileTableSelectionChanged(object recipient, FileTableSelectionChangedMessage message)
    {
        var context = Context;
        if (message.Identity != context.View.Identity)
        {
            return;
        }

        _isParentRowSelected = message.IsParentRowSelected;
        UpdateParentSelectionOpacity(context, queueRetry: true);
    }

    private void UpdateParentSelectionOpacity(FileEntryTableBehaviorContext context, bool queueRetry)
    {
        var table = context.Table;
        if (!_isParentRowSelected)
        {
            ResetParentSelectionOpacity(table);
            return;
        }

        if (table.GetParentItem() is not { } parentItem
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
        QueueParentSelectionOpacityRetry(context, queueRetry);
    }

    private void QueueParentSelectionOpacityRetry(FileEntryTableBehaviorContext context, bool queueRetry)
    {
        if (queueRetry)
        {
            context.View.DispatcherQueue.TryEnqueue(() =>
            {
                if (IsLoaded)
                {
                    UpdateParentSelectionOpacity(context, queueRetry: false);
                }
            });
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
