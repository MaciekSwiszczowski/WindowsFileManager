using WinUiFileManager.Application.Messages.RequestMessages.Navigation;

namespace WinUiFileManager.Presentation.FileEntryTable.Behaviors;

public sealed class ActiveRowIndicatorBehavior : FileEntryTableBehaviorBase
{
    private const string IndicatorName = "ActiveRowIndicator";
    private const double ActiveOpacity = 1d;
    private const double InactiveOpacity = 0d;

    private SpecFileEntryViewModel? _activeItem;
    private PointerEventHandler? _pointerPressedHandler;

    protected override void OnLoaded(FileEntryTableContext context)
    {
        context.View.PreviewKeyDown += OnPreviewKeyDown;
        _pointerPressedHandler = OnPointerPressed;
        context.View.AddHandler(UIElement.PointerPressedEvent, _pointerPressedHandler, handledEventsToo: true);
        context.Messenger.Register(
            this,
            IdentityFilter.For<FileTableSelectionChangedMessage>(context.View.Identity, OnFileTableSelectionChanged));
    }

    protected override void OnUnloaded(FileEntryTableContext context)
    {
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

    private void SetActiveRow(SpecFileEntryViewModel item)
    {
        if (ReferenceEquals(_activeItem, item))
        {
            QueueApplyActiveIndicator();
            return;
        }

        ClearActiveItem();
        _activeItem = item;
        ApplyActiveIndicator();
        QueueApplyActiveIndicator();
    }

    private bool TrySendNavigationMessage(SpecFileEntryViewModel item)
    {
        if (SpecFileEntryViewModel.IsParentEntry(item))
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
        if (_activeItem is { } activeItem)
        {
            SetItemIndicatorOpacity(Context.Table, activeItem, InactiveOpacity);
        }

        _activeItem = null;
    }

    private void QueueApplyActiveIndicator()
    {
        Context.View.DispatcherQueue.TryEnqueue(() =>
        {
            if (IsLoaded)
            {
                ApplyActiveIndicator();
            }
        });
    }

    private void ApplyActiveIndicator()
    {
        if (_activeItem is { } activeItem)
        {
            SetItemIndicatorOpacity(Context.Table, activeItem, ActiveOpacity);
        }
    }

    private static void SetItemIndicatorOpacity(TableView table, SpecFileEntryViewModel item, double opacity)
    {
        if (table.ContainerFromItem(item) is not { } container)
        {
            return;
        }

        SetDescendantIndicatorOpacity(container, opacity);
    }

    private static void SetDescendantIndicatorOpacity(DependencyObject parent, double opacity)
    {
        var childCount = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < childCount; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is FrameworkElement { Name: IndicatorName } indicator)
            {
                indicator.Opacity = opacity;
            }

            SetDescendantIndicatorOpacity(child, opacity);
        }
    }
}
