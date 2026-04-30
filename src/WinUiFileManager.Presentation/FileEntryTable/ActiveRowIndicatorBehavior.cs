using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Xaml.Interactivity;
using WinUiFileManager.Presentation.FileEntryTable.Messages;

namespace WinUiFileManager.Presentation.FileEntryTable;

public sealed class ActiveRowIndicatorBehavior : Behavior<SpecFileEntryTableView>
{
    private const string DefaultIndicatorName = "ActiveRowIndicator";
    private const string ParentTableName = "ParentTable";
    private const string EntryTableName = "EntryTable";

    private SpecFileEntryViewModel? _activeItem;
    private string? _activeTableName;
    private PointerEventHandler? _pointerPressedHandler;
    private bool _refreshQueued;

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
        _activeTableName = null;
        _refreshQueued = false;
        WeakReferenceMessenger.Default.Unregister<FileTableSelectionChangedMessage>(this);

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
        _activeItem = item;
        _activeTableName = table.Name;
        SetDescendantIndicatorOpacity(AssociatedObject, InactiveOpacity);
        SetItemIndicatorOpacity(table, item, ActiveOpacity);
    }

    private void OnFileTableSelectionChanged(object recipient, FileTableSelectionChangedMessage message)
    {
        if (AssociatedObject is null || message.Identity != AssociatedObject.Identity)
        {
            return;
        }

        if (message.IsParentRowSelected)
        {
            ActivateParentRow();
            return;
        }

        if (message.SelectedItems.LastOrDefault() is not { } item)
        {
            ClearActiveItem();
            return;
        }

        ActivateMessageItem(EntryTableName, item);
    }

    private void ActivateParentRow()
    {
        if (AssociatedObject?.ParentRows.FirstOrDefault() is not { } item)
        {
            ClearActiveItem();
            return;
        }

        ActivateMessageItem(ParentTableName, item);
    }

    private void ActivateMessageItem(string tableName, SpecFileEntryViewModel item)
    {
        if (AssociatedObject is null || FindTable(tableName) is not { } table)
        {
            return;
        }

        ActivateItem(table, item);
        QueueRefresh();
    }

    private void ClearActiveItem()
    {
        _activeItem = null;
        _activeTableName = null;

        if (AssociatedObject is not null)
        {
            SetDescendantIndicatorOpacity(AssociatedObject, InactiveOpacity);
        }
    }

    private void QueueRefresh()
    {
        if (_refreshQueued || AssociatedObject is null)
        {
            return;
        }

        _refreshQueued = true;
        AssociatedObject.DispatcherQueue.TryEnqueue(() =>
        {
            _refreshQueued = false;
            RefreshActiveItem();
        });
    }

    private void RefreshActiveItem()
    {
        if (_activeItem is null || _activeTableName is null || FindTable(_activeTableName) is not { } table)
        {
            return;
        }

        SetDescendantIndicatorOpacity(AssociatedObject, InactiveOpacity);
        SetItemIndicatorOpacity(table, _activeItem, ActiveOpacity);
    }

    private TableView? FindTable(string name) =>
        AssociatedObject is null
            ? null
            : FindDescendantTable(AssociatedObject, name);

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

    private static TableView? FindDescendantTable(DependencyObject parent, string name)
    {
        var childCount = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < childCount; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is TableView { Name: var tableName } table && tableName == name)
            {
                return table;
            }

            if (FindDescendantTable(child, name) is { } match)
            {
                return match;
            }
        }

        return null;
    }
}
