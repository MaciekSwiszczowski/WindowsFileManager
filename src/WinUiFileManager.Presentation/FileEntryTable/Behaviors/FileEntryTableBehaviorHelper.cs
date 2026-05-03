namespace WinUiFileManager.Presentation.FileEntryTable.Behaviors;

internal static class FileEntryTableBehaviorHelper
{
    private const double DefaultRowHeight = 32d;

    public static bool IsModifierDown(VirtualKey key) =>
        InputKeyboardSource.GetKeyStateForCurrentThread(key)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

    public static bool HasAnyModifier(params VirtualKey[] keys) =>
        keys.Any(IsModifierDown);

    public static bool EnsureTable(
        SpecFileEntryTableView? view,
        ref TableView? currentTable,
        Action detachEvents,
        Action attachEvents)
    {
        if (view is null)
        {
            return false;
        }

        var table = view.Table;
        if (!ReferenceEquals(currentTable, table))
        {
            detachEvents();
            currentTable = table;
        }

        attachEvents();
        return true;
    }

    public static int? ClampIndex(TableView table, int? index)
    {
        if (table.Items.Count == 0 || index is null)
        {
            return null;
        }

        return Math.Clamp(index.Value, 0, table.Items.Count - 1);
    }

    public static int GetCurrentSelectedIndex(TableView table)
    {
        if (table.SelectedIndex >= 0)
        {
            return table.SelectedIndex;
        }

        if (table.SelectedItem is not null)
        {
            var selectedItemIndex = table.Items.IndexOf(table.SelectedItem);
            if (selectedItemIndex >= 0)
            {
                return selectedItemIndex;
            }
        }

        foreach (var item in table.SelectedItems.Reverse())
        {
            var selectedIndex = table.Items.IndexOf(item);
            if (selectedIndex >= 0)
            {
                return selectedIndex;
            }
        }

        return 0;
    }

    public static int? GetRowIndex(TableView table, SpecFileEntryViewModel? item)
    {
        if (item is null)
        {
            return null;
        }

        var index = table.Items.IndexOf(item);
        return index >= 0 ? index : null;
    }

    public static bool TryGetNavigationTargetIndex(
        TableView table,
        VirtualKey key,
        int currentIndex,
        out int targetIndex)
    {
        targetIndex = key switch
        {
            VirtualKey.Up => currentIndex - 1,
            VirtualKey.Down => currentIndex + 1,
            VirtualKey.Home => 0,
            VirtualKey.End => table.Items.Count - 1,
            VirtualKey.PageUp => GetPageTargetIndex(table, currentIndex, pageUp: true),
            VirtualKey.PageDown => GetPageTargetIndex(table, currentIndex, pageUp: false),
            _ => currentIndex,
        };

        if (key is not (VirtualKey.Up
            or VirtualKey.Down
            or VirtualKey.Home
            or VirtualKey.End
            or VirtualKey.PageUp
            or VirtualKey.PageDown))
        {
            return false;
        }

        targetIndex = ClampIndex(table, targetIndex) ?? currentIndex;
        return true;
    }

    public static bool TryGetRangeTargetIndex(
        TableView table,
        VirtualKey key,
        int cursorIndex,
        out int targetIndex)
    {
        targetIndex = key switch
        {
            VirtualKey.Up => cursorIndex - 1,
            VirtualKey.Down => cursorIndex + 1,
            VirtualKey.Home => 0,
            VirtualKey.End => table.Items.Count - 1,
            VirtualKey.PageUp => GetPageTargetIndex(table, cursorIndex, pageUp: true),
            VirtualKey.PageDown => GetPageTargetIndex(table, cursorIndex, pageUp: false),
            _ => cursorIndex,
        };

        if (key is not (VirtualKey.Up
            or VirtualKey.Down
            or VirtualKey.Home
            or VirtualKey.End
            or VirtualKey.PageUp
            or VirtualKey.PageDown))
        {
            return false;
        }

        targetIndex = ClampIndex(table, targetIndex) ?? cursorIndex;
        return true;
    }

    public static void SelectSingleRow(
        TableView table,
        FileEntryTableNavigationState navigationState,
        int targetIndex)
    {
        if (table.Items[targetIndex] is not { } item)
        {
            return;
        }

        navigationState.SetCurrent(table, targetIndex, resetSelectionAnchor: true);
        if (table.SelectedItems.Count == 1 && ReferenceEquals(table.SelectedItems[0], item))
        {
            ScrollRowIntoViewIfNeeded(table, targetIndex);
            return;
        }

        table.SelectedItems.Clear();
        table.SelectedItems.Add(item);
        ScrollRowIntoViewIfNeeded(table, targetIndex);
    }

    public static void ScrollRowIntoViewIfNeeded(TableView table, int targetIndex)
    {
        var visibleRange = GetVisibleRowRange(table);
        if (targetIndex < visibleRange.FirstIndex || targetIndex > visibleRange.LastIndex)
        {
            table.ScrollRowIntoView(targetIndex);
        }
    }

    public static SpecFileEntryViewModel? GetParentItem(TableView table) =>
        table.Items.Count > 0 && table.Items[0] is SpecFileEntryViewModel parentItem
            ? parentItem
            : null;

    public static FileEntryColumn? MapColumn(string? sortMemberPath) =>
        sortMemberPath switch
        {
            nameof(SpecFileEntryViewModel.Name) => FileEntryColumn.Name,
            nameof(SpecFileEntryViewModel.Extension) => FileEntryColumn.Extension,
            nameof(SpecFileEntryViewModel.Size) => FileEntryColumn.Size,
            nameof(SpecFileEntryViewModel.Modified) => FileEntryColumn.Modified,
            nameof(SpecFileEntryViewModel.Attributes) => FileEntryColumn.Attributes,
            _ => null,
        };

    public static string MapSortMemberPath(FileEntryColumn column) =>
        column switch
        {
            FileEntryColumn.Name => nameof(SpecFileEntryViewModel.Name),
            FileEntryColumn.Extension => nameof(SpecFileEntryViewModel.Extension),
            FileEntryColumn.Size => nameof(SpecFileEntryViewModel.Size),
            FileEntryColumn.Modified => nameof(SpecFileEntryViewModel.Modified),
            FileEntryColumn.Attributes => nameof(SpecFileEntryViewModel.Attributes),
            _ => nameof(SpecFileEntryViewModel.Name),
        };

    private static int GetPageTargetIndex(TableView table, int currentIndex, bool pageUp)
    {
        var visibleRange = GetVisibleRowRange(table);
        if (pageUp)
        {
            return currentIndex > visibleRange.FirstIndex && currentIndex <= visibleRange.LastIndex
                ? visibleRange.FirstIndex
                : currentIndex - visibleRange.Count;
        }

        return currentIndex >= visibleRange.FirstIndex && currentIndex < visibleRange.LastIndex
            ? visibleRange.LastIndex
            : currentIndex + visibleRange.Count;
    }

    private static VisibleRowRange GetVisibleRowRange(TableView table)
    {
        var rowHeight = GetEffectiveRowHeight(table);
        var scrollViewer = FindDescendant<ScrollViewer>(table);
        var verticalOffset = scrollViewer?.VerticalOffset ?? table.VerticalOffset;
        var viewportHeight = scrollViewer?.ViewportHeight is > 0
            ? scrollViewer.ViewportHeight
            : table.ActualHeight;
        var firstIndex = (int)Math.Floor(verticalOffset / rowHeight);
        var visibleCount = Math.Max(1, (int)Math.Floor(viewportHeight / rowHeight));

        firstIndex = ClampIndex(table, firstIndex) ?? 0;
        var lastIndex = ClampIndex(table, firstIndex + visibleCount - 1) ?? firstIndex;
        return new VisibleRowRange(firstIndex, lastIndex, Math.Max(1, lastIndex - firstIndex + 1));
    }

    private static double GetEffectiveRowHeight(TableView table)
    {
        if (!double.IsNaN(table.RowHeight) && table.RowHeight > 0)
        {
            return table.RowHeight;
        }

        if (FindDescendant<TableViewRow>(table) is { ActualHeight: > 0 } row)
        {
            return row.ActualHeight;
        }

        return DefaultRowHeight;
    }

    private static T? FindDescendant<T>(DependencyObject parent)
        where T : DependencyObject
    {
        var childCount = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < childCount; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match)
            {
                return match;
            }

            if (FindDescendant<T>(child) is { } descendant)
            {
                return descendant;
            }
        }

        return null;
    }

    private sealed record VisibleRowRange(int FirstIndex, int LastIndex, int Count);
}
