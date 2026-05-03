namespace WinUiFileManager.Presentation.FileEntryTable;

public sealed class FileEntryTableNavigationState
{
    private SpecFileEntryViewModel? _currentItem;
    private int? _currentIndex;
    private int? _selectionAnchorIndex;
    private int? _selectionCursorIndex;

    public void Reset()
    {
        _currentItem = null;
        _currentIndex = null;
        _selectionAnchorIndex = null;
        _selectionCursorIndex = null;
    }

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

    public int? GetCurrentIndex(TableView table)
    {
        if (ClampIndex(table, _currentIndex) is { } currentIndex)
        {
            return currentIndex;
        }

        if (_currentItem is not null &&
            table.Items.IndexOf(_currentItem) is var itemIndex and >= 0)
        {
            return itemIndex;
        }

        return null;
    }

    public int? GetSelectionAnchorIndex(TableView table) => ClampIndex(table, _selectionAnchorIndex);

    public int? GetSelectionCursorIndex(TableView table) => ClampIndex(table, _selectionCursorIndex);

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
