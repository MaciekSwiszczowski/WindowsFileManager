using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Xaml.Interactivity;
using WinUiFileManager.Presentation.FileEntryTable.Messages;

namespace WinUiFileManager.Presentation.FileEntryTable;

public sealed class ActiveRowIndicatorBehavior : Behavior<SpecFileEntryTableView>
{
    private const string DefaultIndicatorName = "ActiveRowIndicator";
    private const string EntryTableName = "EntryTable";

    private SpecFileEntryViewModel? _activeItem;
    private PointerEventHandler? _pointerPressedHandler;

    private const double ActiveOpacity = 1d;
    private const double InactiveOpacity = 0d;

    protected override void OnAttached()
    {
        base.OnAttached();

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
        if (_activeItem is { } activeItem && FindTable() is { } table)
        {
            SetItemIndicatorOpacity(table, activeItem, ActiveOpacity);
        }
    }

    private TableView? FindTable() =>
        AssociatedObject.FindDescendant<TableView>(static table => table.Name == EntryTableName);

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
