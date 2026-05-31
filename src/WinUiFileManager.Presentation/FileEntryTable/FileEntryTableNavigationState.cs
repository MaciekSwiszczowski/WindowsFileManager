namespace WinUiFileManager.Presentation.FileEntryTable;

/// <summary>
/// Mutable per-table navigation/selection cursor state that the stock <see cref="TableView"/> does not
/// track itself: the "current" focused row plus the Shift-selection anchor and cursor indices. Shared
/// (via <see cref="Behaviors.FileEntryTableContext"/>) between the keyboard navigation and selection
/// behaviors so they agree on where the cursor is.
/// </summary>
/// <remarks>
/// Holds both an item reference and an index; the item is the fallback identity when indices shift
/// after the collection changes (e.g. a sort or refresh). All accessors re-clamp against the live item
/// count, so stale indices degrade gracefully instead of throwing. UI-thread affinity: mutated only
/// from the table's event handlers on the UI thread.
/// </remarks>
public sealed class FileEntryTableNavigationState
{
    // The focused row's last-known item and index. _currentItem survives index shifts (re-sort/refresh).
    private SpecFileEntryViewModel? _currentItem;
    private int? _currentIndex;
    // Shift-range endpoints: anchor is where the range started, cursor is the moving end.
    private int? _selectionAnchorIndex;
    private int? _selectionCursorIndex;

    private void Reset()
    {
        _currentItem = null;
        _currentIndex = null;
        _selectionAnchorIndex = null;
        _selectionCursorIndex = null;
    }

    /// <summary>Sets the current/focused row to <paramref name="index"/> (clamped). When
    /// <paramref name="resetSelectionAnchor"/> is true the Shift-selection anchor is moved here too,
    /// starting a fresh range. Resets all state if the index is out of range / table empty.</summary>
    public void SetCurrent(TableView table, int index, bool resetSelectionAnchor)
    {
        var currentIndex = ClampIndex(table, index);
        if (currentIndex is null)
        {
            Reset();
            return;
        }

        SetCurrent(table.Items[currentIndex.Value] as SpecFileEntryViewModel, currentIndex, resetSelectionAnchor);
    }

    private void SetCurrent(SpecFileEntryViewModel? item, int? index, bool resetSelectionAnchor)
    {
        _currentItem = item;
        _currentIndex = index;
        _selectionCursorIndex = index;

        if (resetSelectionAnchor)
        {
            _selectionAnchorIndex = index;
        }
    }

    /// <summary>Records an explicit Shift-selection range (anchor + moving cursor), clamping both and
    /// syncing the current item/index to the cursor end. Resets state if the cursor is invalid.</summary>
    public void SetSelectionRange(TableView table, int anchorIndex, int cursorIndex)
    {
        var currentIndex = ClampIndex(table, cursorIndex);
        if (currentIndex is null)
        {
            Reset();
            return;
        }

        _selectionAnchorIndex = ClampIndex(table, anchorIndex) ?? currentIndex;
        _selectionCursorIndex = currentIndex;
        _currentIndex = currentIndex;
        _currentItem = table.Items[currentIndex.Value] as SpecFileEntryViewModel;
    }

    /// <summary>Resolves the current row index against the live table: uses the clamped stored index,
    /// else re-locates the stored item (handles rows shifting after a sort/refresh), else null.</summary>
    public int? GetCurrentIndex(TableView table)
    {
        if (ClampIndex(table, _currentIndex) is { } currentIndex)
        {
            return currentIndex;
        }

        // Index went stale (collection changed); fall back to locating the remembered item.
        if (_currentItem is not null &&
            table.Items.IndexOf(_currentItem) is var itemIndex and >= 0)
        {
            return itemIndex;
        }

        return null;
    }

    /// <summary>The Shift-selection anchor index, clamped to the live table, or null.</summary>
    public int? GetSelectionAnchorIndex(TableView table) => ClampIndex(table, _selectionAnchorIndex);

    /// <summary>The Shift-selection moving cursor index, clamped to the live table, or null.</summary>
    public int? GetSelectionCursorIndex(TableView table) => ClampIndex(table, _selectionCursorIndex);

    /// <summary>The current focused row resolved against the live table; falls back to the stored item
    /// reference when the index can no longer be resolved.</summary>
    public SpecFileEntryViewModel? GetCurrentItem(TableView table)
    {
        if (GetCurrentIndex(table) is { } currentIndex)
        {
            return table.Items[currentIndex] as SpecFileEntryViewModel;
        }

        return _currentItem;
    }

    private static int? ClampIndex(TableView table, int? index)
    {
        if (index is null || table.Items.Count == 0)
        {
            return null;
        }

        return Math.Clamp(index.Value, 0, table.Items.Count - 1);
    }
}
