using Microsoft.Xaml.Interactivity;

namespace WinUiFileManager.Presentation.FileEntryTable;

public sealed class ActiveRowIndicatorBehavior : Behavior<SpecFileEntryTableView>
{
    private const string DefaultIndicatorName = "ActiveRowIndicator";

    private PointerEventHandler? _pointerPressedHandler;

    private const double ActiveOpacity = 1d;
    private const double InactiveOpacity = 0.5d;

    protected override void OnAttached()
    {
        base.OnAttached();

        _pointerPressedHandler = OnPointerPressed;
        AssociatedObject.AddHandler(UIElement.PointerPressedEvent, _pointerPressedHandler, handledEventsToo: true);
    }

    protected override void OnDetaching()
    {
        if (AssociatedObject is { } table && _pointerPressedHandler is not null)
        {
            table.RemoveHandler(UIElement.PointerPressedEvent, _pointerPressedHandler);
        }

        _pointerPressedHandler = null;

        base.OnDetaching();
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var source = e.OriginalSource as DependencyObject;
        if (!e.IsPrimaryPointerPress() ||
            AssociatedObject is null ||
            source.FindAncestor<TableView>() is not { } table ||
            source.FindItem() is not { } item)
        {
            return;
        }

        ActivateItem(table, item);
    }

    private void ActivateItem(TableView table, SpecFileEntryViewModel item)
    {
        SetDescendantIndicatorOpacity(AssociatedObject, InactiveOpacity);
        SetItemIndicatorOpacity(table, item, ActiveOpacity);
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
