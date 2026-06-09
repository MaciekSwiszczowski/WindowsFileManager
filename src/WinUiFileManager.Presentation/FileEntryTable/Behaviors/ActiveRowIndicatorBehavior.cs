using WinUiFileManager.Application.Messages.RequestMessages.Navigation;
using WinUiFileManager.Presentation.FileEntryTable;

namespace WinUiFileManager.Presentation.FileEntryTable.Behaviors;

/// <summary>
/// Tracks the "active" row (the one the user last clicked or that selection moved to) and toggles the
/// opacity of the per-row <c>ActiveRowIndicator</c> element so it is only visible on that row. Also
/// turns an Enter press on the active row into an up/down navigation request.
/// </summary>
/// <remarks>
/// Pane-scoped behavior: it listens for <see cref="FileTableSelectionChangedMessage"/> filtered by the
/// view's pane <see cref="FileEntryTableContext.View"/> identity through the messenger wrapper (AGENTS.md §4).
/// <para>
/// Event discipline (AGENTS.md §5): <see cref="OnLoaded"/> subscribes <c>PreviewKeyDown</c> and adds a
/// <see cref="UIElement.PointerPressedEvent"/> handler (with <c>handledEventsToo</c> so it still sees
/// presses the table marks handled); both are reversed in <see cref="OnUnloaded"/>. Messenger
/// unregistration is handled by the base class.
/// </para>
/// <para>
/// Indicator opacity is applied directly to the realised container, so it must be re-applied after
/// virtualization recycles/realises rows — hence <see cref="QueueApplyActiveIndicator"/> re-runs the
/// update on the dispatcher once layout has settled.
/// </para>
/// </remarks>
public sealed class ActiveRowIndicatorBehavior : FileEntryTableBehaviorBase
{
    private FileListingRow? _activeItem;
    private PointerEventHandler? _pointerPressedHandler;

    protected override void OnLoaded(FileEntryTableContext context)
    {
        context.View.PreviewKeyDown += OnPreviewKeyDown;
        // Keep a field reference to the handler so the exact same delegate instance can be removed in
        // OnUnloaded (AddHandler/RemoveHandler require delegate identity).
        _pointerPressedHandler = OnPointerPressed;
        // handledEventsToo: true so we still observe presses even after the TableView marks them handled.
        context.View.AddHandler(UIElement.PointerPressedEvent, _pointerPressedHandler, handledEventsToo: true);
        context.Messenger.Register<FileTableSelectionChangedMessage>(this, context.View.Identity, OnFileTableSelectionChanged);
    }

    protected override void OnUnloaded(FileEntryTableContext context)
    {
        // Reverse the AddHandler from OnLoaded using the stored delegate instance.
        if (_pointerPressedHandler is not null)
        {
            context.View.RemoveHandler(UIElement.PointerPressedEvent, _pointerPressedHandler);
        }

        context.View.PreviewKeyDown -= OnPreviewKeyDown;
        _pointerPressedHandler = null;
        ClearActiveItem();
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var source = e.OriginalSource as DependencyObject;
        if (!e.IsPrimaryPointerPress()
            || source.FindAncestor<TableView>() is null
            || source.FindItem() is not { } item)
        {
            return;
        }

        SetActiveRow(item);
    }

    private void OnPreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Handled || e.Key != VirtualKey.Enter || _activeItem is not { } activeItem)
        {
            return;
        }

        if (TrySendNavigationMessage(activeItem))
        {
            e.Handled = true;
        }
    }

    private void OnFileTableSelectionChanged(FileTableSelectionChangedMessage message)
    {
        if (message.ActiveItem is not { } item)
        {
            ClearActiveItem();
            return;
        }

        SetActiveRow(item);
    }

    private void SetActiveRow(FileListingRow item)
    {
        _activeItem = item;
        // Publish to the shared tracker; the per-row x:Bind indicator re-evaluates and shows on the matching row.
        // No visual-tree walk (the old approach churned COM wrappers per node on every selection).
        ActiveRowTracker.Instance.SetActive(Context.View.Identity, item);
    }

    private bool TrySendNavigationMessage(FileListingRow item)
    {
        if (FileListingRow.IsParentEntry(item))
        {
            Context.Messenger.Send(new FileTableNavigateUpRequestedMessage(Context.View.Identity));
            return true;
        }

        if (item.Model is { Kind: ItemKind.Directory } model)
        {
            Context.Messenger.Send(new FileTableNavigateDownRequestedMessage(Context.View.Identity, model.Name));
            return true;
        }

        return false;
    }

    private void ClearActiveItem()
    {
        _activeItem = null;
        ActiveRowTracker.Instance.SetActive(Context.View.Identity, null);
    }
}
