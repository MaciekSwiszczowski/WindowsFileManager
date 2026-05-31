namespace WinUiFileManager.Presentation.FileEntryTable.Behaviors;

/// <summary>
/// Visually de-emphasises the synthetic ".." parent row when it is part of the current selection by
/// dimming its opacity, so a range selection that happens to include the parent row reads as
/// "everything below the parent" rather than highlighting a non-file navigation row at full strength.
/// </summary>
/// <remarks>
/// Pane-scoped via <c>IdentityFilter.For&lt;FileTableSelectionChangedMessage&gt;</c> on the view
/// identity (AGENTS.md §4). Opacity is set on the realised <see cref="TableViewRow"/> container, so it
/// must be re-applied after virtualization — hence the queued retry in
/// <see cref="QueueParentSelectionOpacityRetry"/>. <see cref="OnUnloaded"/> restores the parent row's
/// opacity so a recycled container is not left dimmed.
/// </remarks>
public sealed class ParentRowSelectionOpacityBehavior : FileEntryTableBehaviorBase
{
    private const double ParentSelectionOpacity = 0.5d;
    private const double DefaultSelectionOpacity = 1d;

    // The parent row we last dimmed, so we know which container to restore.
    private SpecFileEntryViewModel? _dimmedParentItem;
    private bool _isParentRowSelected;

    protected override void OnLoaded(FileEntryTableContext context) =>
        context.Messenger.Register(
            this,
            IdentityFilter.For<FileTableSelectionChangedMessage>(context.View.Identity, OnFileTableSelectionChanged));

    protected override void OnUnloaded(FileEntryTableContext context)
    {
        // Undo any dimming so a recycled row container does not carry stale opacity.
        ResetParentSelectionOpacity(context.Table);
        _dimmedParentItem = null;
        _isParentRowSelected = false;
    }

    private void OnFileTableSelectionChanged(FileTableSelectionChangedMessage message)
    {
        var context = Context;

        _isParentRowSelected = message.IsParentRowSelected;
        UpdateParentSelectionOpacity(context, queueRetry: true);
    }

    private void UpdateParentSelectionOpacity(FileEntryTableContext context, bool queueRetry)
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

    /// <summary>Re-runs the opacity update once on the dispatcher. The parent row's container may not
    /// be realised when the selection message arrives (virtualization), so a single deferred retry
    /// catches the case where the container appears immediately after. The <c>queueRetry=false</c>
    /// re-entry prevents an infinite retry loop.</summary>
    private void QueueParentSelectionOpacityRetry(FileEntryTableContext context, bool queueRetry)
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
