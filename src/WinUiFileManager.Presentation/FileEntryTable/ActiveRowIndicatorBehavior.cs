using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Xaml.Interactivity;
using WinUiFileManager.Presentation.FileEntryTable.Messages;

namespace WinUiFileManager.Presentation.FileEntryTable;

public sealed class ActiveRowIndicatorBehavior : Behavior<SpecFileEntryTableView>
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
        _activeItem = item;
        ApplyActiveRow();
    }

    private bool TrySendNavigationMessage(SpecFileEntryViewModel item)
    {
        if (AssociatedObject is null)
        {
            return false;
        }

        if (item.Model is null)
        {
            WeakReferenceMessenger.Default.Send(new FileTableNavigateUpRequestedMessage(AssociatedObject.Identity));
            return true;
        }

        if (item.EntryKind == FileEntryKind.Folder)
        {
            WeakReferenceMessenger.Default.Send(new FileTableNavigateDownRequestedMessage(AssociatedObject.Identity, item));
            return true;
        }

        return false;
    }

    private void ClearActiveItem()
    {
        _activeItem = null;

        if (AssociatedObject is not null)
        {
            SetDescendantIndicatorOpacity(AssociatedObject, InactiveOpacity);
        }
    }

    private void ApplyActiveRow()
    {
        if (AssociatedObject is null)
        {
            return;
        }

        SetDescendantIndicatorOpacity(AssociatedObject, InactiveOpacity);
        if (_activeItem is { } activeItem)
        {
            SetItemIndicatorOpacity(AssociatedObject.Table, activeItem, ActiveOpacity);
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
            if (child is FrameworkElement { Name: DefaultIndicatorName } element)
            {
                element.Opacity = opacity;
            }

            SetDescendantIndicatorOpacity(child, opacity);
        }
    }
}
