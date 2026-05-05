namespace WinUiFileManager.Presentation.FileEntryTable.Behaviors;

public sealed class ActiveRowIndicatorBehavior : FileEntryTableBehavior
{
    private const string DefaultIndicatorName = "ActiveRowIndicator";

    private SpecFileEntryViewModel? _activeItem;
    private PointerEventHandler? _pointerPressedHandler;

    private const double ActiveOpacity = 1d;
    private const double InactiveOpacity = 0d;

    protected override void OnAttached()
    {
        base.OnAttached();

        AssociatedObject.PreviewKeyDown += OnPreviewKeyDown;
        _pointerPressedHandler = OnPointerPressed;
        AssociatedObject.AddHandler(UIElement.PointerPressedEvent, _pointerPressedHandler, handledEventsToo: true);
        WeakReferenceMessenger.Default.Register<FileTableSelectionChangedMessage>(this, OnFileTableSelectionChanged);
    }

    protected override void OnDetaching()
    {
        if (AssociatedObject is { } table && _pointerPressedHandler is not null)
        {
            table.RemoveHandler(UIElement.PointerPressedEvent, _pointerPressedHandler);
        }

        if (AssociatedObject is { } view)
        {
            view.PreviewKeyDown -= OnPreviewKeyDown;
        }

        _pointerPressedHandler = null;
        _activeItem = null;
        WeakReferenceMessenger.Default.Unregister<FileTableSelectionChangedMessage>(this);

        base.OnDetaching();
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var source = e.OriginalSource as DependencyObject;
        if (!e.IsPrimaryPointerPress() ||
            AssociatedObject is null ||
            source.FindAncestor<TableView>() is null ||
            source.FindItem() is not { } item)
        {
            return;
        }

        SetActiveRow(item);
    }

    private void OnPreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Handled || e.Key != VirtualKey.Enter || AssociatedObject is null || _activeItem is not { } activeItem)
        {
            return;
        }

        if (TrySendNavigationMessage(activeItem))
        {
            e.Handled = true;
        }
    }

    private void OnFileTableSelectionChanged(object recipient, FileTableSelectionChangedMessage message)
    {
        if (AssociatedObject is null || message.Identity != AssociatedObject.Identity)
        {
            return;
        }

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
            return;
        }

        if (AssociatedObject is { } view && _activeItem is { } previousActiveItem)
        {
            SetItemIndicatorOpacity(view.Table, previousActiveItem, InactiveOpacity);
        }

        _activeItem = item;
        ApplyActiveItemIndicator();
    }

    private bool TrySendNavigationMessage(SpecFileEntryViewModel item)
    {
        if (AssociatedObject is null)
        {
            return false;
        }

        if (SpecFileEntryViewModel.IsParentEntry(item))
        {
            WeakReferenceMessenger.Default.Send(new FileTableNavigateUpRequestedMessage(AssociatedObject.Identity));
            return true;
        }

        if (item is { EntryKind: FileEntryKind.Folder, Model: { } model })
        {
            WeakReferenceMessenger.Default.Send(new FileTableNavigateDownRequestedMessage(AssociatedObject.Identity, model));
            return true;
        }

        return false;
    }

    private void ClearActiveItem()
    {
        if (AssociatedObject is { } view && _activeItem is { } previousActiveItem)
        {
            SetItemIndicatorOpacity(view.Table, previousActiveItem, InactiveOpacity);
        }

        _activeItem = null;
    }

    private void ApplyActiveItemIndicator()
    {
        if (AssociatedObject is null || _activeItem is not { } activeItem)
        {
            return;
        }

        SetItemIndicatorOpacity(AssociatedObject.Table, activeItem, ActiveOpacity);
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
            if (child is FrameworkElement { Name: DefaultIndicatorName } element)
            {
                element.Opacity = opacity;
            }

            SetDescendantIndicatorOpacity(child, opacity);
        }
    }
}
