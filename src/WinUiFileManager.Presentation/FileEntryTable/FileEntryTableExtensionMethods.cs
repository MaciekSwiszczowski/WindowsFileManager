namespace WinUiFileManager.Presentation.FileEntryTable;

internal static class FileEntryTableExtensionMethods
{
    private const double DefaultRowHeight = 32d;

    extension(TableView table)
    {
        public int? ClampIndex(int? index)
        {
            if (table.Items.Count == 0 || index is null)
            {
                return null;
            }

            return Math.Clamp(index.Value, 0, table.Items.Count - 1);
        }
        public int GetCurrentSelectedIndex()
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
        public int? GetRowIndex(SpecFileEntryViewModel? item)
        {
            if (item is null)
            {
                return null;
            }

            var index = table.Items.IndexOf(item);
            return index >= 0 ? index : null;
        }
        public bool TryGetNavigationTargetIndex(VirtualKey key,
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

            targetIndex = table.ClampIndex(targetIndex) ?? currentIndex;
            return true;
        }
        public bool TryGetRangeTargetIndex(VirtualKey key,
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

            targetIndex = table.ClampIndex(targetIndex) ?? cursorIndex;
            return true;
        }
        public void SelectSingleRow(FileEntryTableNavigationState navigationState,
            int targetIndex)
        {
            if (table.Items[targetIndex] is not { } item)
            {
                return;
            }

            navigationState.SetCurrent(table, targetIndex, resetSelectionAnchor: true);
            if (table.SelectedItems.Count == 1 && ReferenceEquals(table.SelectedItems[0], item))
            {
                table.ScrollRowIntoViewIfNeeded(targetIndex);
                return;
            }

            table.SelectedItems.Clear();
            table.SelectedItems.Add(item);
            table.ScrollRowIntoViewIfNeeded(targetIndex);
        }
        public void ScrollRowIntoViewIfNeeded(int targetIndex)
        {
            var visibleRange = GetVisibleRowRange(table);
            if (targetIndex < visibleRange.FirstIndex || targetIndex > visibleRange.LastIndex)
            {
                table.ScrollRowIntoView(targetIndex);
            }
        }
        public SpecFileEntryViewModel? GetParentItem() =>
            table.Items.Count > 0 && table.Items[0] is SpecFileEntryViewModel parentItem
                ? parentItem
                : null;
    }

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
        var scrollViewer = WinUiViewHelper.FindDescendant<ScrollViewer>(table);
        var verticalOffset = scrollViewer?.VerticalOffset ?? table.VerticalOffset;
        var viewportHeight = scrollViewer?.ViewportHeight is > 0
            ? scrollViewer.ViewportHeight
            : table.ActualHeight;
        var firstIndex = (int)Math.Floor(verticalOffset / rowHeight);
        var visibleCount = Math.Max(1, (int)Math.Floor(viewportHeight / rowHeight));

        firstIndex = table.ClampIndex(firstIndex) ?? 0;
        var lastIndex = table.ClampIndex(firstIndex + visibleCount - 1) ?? firstIndex;
        return new VisibleRowRange(firstIndex, lastIndex, Math.Max(1, lastIndex - firstIndex + 1));
    }

    private static double GetEffectiveRowHeight(TableView table)
    {
        if (!double.IsNaN(table.RowHeight) && table.RowHeight > 0)
        {
            return table.RowHeight;
        }

        if (WinUiViewHelper.FindDescendant<TableViewRow>(table) is { ActualHeight: > 0 } row)
        {
            return row.ActualHeight;
        }

        return DefaultRowHeight;
    }

    private sealed record VisibleRowRange(int FirstIndex, int LastIndex, int Count);
}
