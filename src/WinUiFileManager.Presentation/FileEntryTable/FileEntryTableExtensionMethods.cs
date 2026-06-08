namespace WinUiFileManager.Presentation.FileEntryTable;

/// <summary>
/// Index/selection/scroll helpers on <see cref="TableView"/> shared by the table keyboard behaviors.
/// Centralises the arithmetic for non-arrow navigation, Shift-selection navigation, row lookup, and
/// "scroll into view only when off-screen" so the behaviors stay focused on gesture handling.
/// </summary>
/// <remarks>
/// Page navigation and "is the row visible?" checks need the current visible row range, which is
/// derived from the realised <see cref="ScrollViewer"/> offset and an estimated row height
/// (<see cref="DefaultRowHeight"/> as a fallback). Because the list is virtualised (AGENTS.md §3) the
/// range is computed from offsets/heights rather than from realised containers, which may not exist for
/// off-screen rows.
/// </remarks>
internal static class FileEntryTableExtensionMethods
{
    private const double DefaultRowHeight = 32d;

    extension(TableView table)
    {
        /// Clamps <paramref name="index"/> into the valid row range, or null when the index is null or the table is empty.
        public int? ClampIndex(int? index)
        {
            if (table.Items.Count == 0 || index is null)
            {
                return null;
            }

            return Math.Clamp(index.Value, 0, table.Items.Count - 1);
        }
        /// <summary>Returns the row index of <paramref name="item"/>, or null if it is null or not
        /// present in the table.</summary>
        public int? GetRowIndex(FileListingRow? item)
        {
            if (item is null)
            {
                return null;
            }

            var index = table.Items.IndexOf(item);
            return index >= 0 ? index : null;
        }

        /// <summary>Computes the destination row for Shift-selection movement relative to
        /// <paramref name="cursorIndex"/>.</summary>
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

        /// <summary>Computes the destination row for non-arrow navigation keys handled outside native TableView.</summary>
        public bool TryGetJumpNavigationTargetIndex(VirtualKey key, int currentIndex, out int targetIndex)
        {
            targetIndex = key switch
            {
                VirtualKey.Home => 0,
                VirtualKey.End => table.Items.Count - 1,
                VirtualKey.PageUp => GetPageTargetIndex(table, currentIndex, pageUp: true),
                VirtualKey.PageDown => GetPageTargetIndex(table, currentIndex, pageUp: false),
                _ => currentIndex,
            };

            if (key is not (VirtualKey.Home or VirtualKey.End or VirtualKey.PageUp or VirtualKey.PageDown))
            {
                return false;
            }

            targetIndex = table.ClampIndex(targetIndex) ?? currentIndex;
            return true;
        }

        /// <summary>Scrolls the row into view only when it is currently outside the visible range,
        /// avoiding gratuitous scrolling/jitter when navigating within the viewport.</summary>
        public void ScrollRowIntoViewIfNeeded(int targetIndex)
        {
            var visibleRange = GetVisibleRowRange(table);
            if (targetIndex < visibleRange.FirstIndex || targetIndex > visibleRange.LastIndex)
            {
                // Fire-and-forget UI scroll; this helper is synchronous and the scroll has no result to await.
                _ = table.ScrollRowIntoView(targetIndex);
            }
        }

        /// <summary>Returns the first row when it is the synthetic ".." parent row, else null.</summary>
        public FileListingRow? GetParentItem() =>
            table.Items.Count > 0 && table.Items[0] is FileListingRow parentItem
                ? parentItem
                : null;
    }

    /// <summary>PageUp/PageDown target: jump to the first/last visible row when the cursor is inside the
    /// viewport and not already at that edge; otherwise move by a full page (the visible row count).</summary>
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

    /// <summary>Estimates the first/last visible row indices and count from the scroll offset, viewport
    /// height, and effective row height. Derived from offsets rather than realised containers because
    /// virtualization means off-screen rows have no container.</summary>
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

    /// <summary>Resolves a row height to use for visible-range math: the explicit <c>RowHeight</c> if
    /// set, else the measured height of any realised row, else <see cref="DefaultRowHeight"/>.</summary>
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

    /// <summary>The inclusive range of currently visible rows and how many are visible.</summary>
    private sealed record VisibleRowRange(int FirstIndex, int LastIndex, int Count);
}
