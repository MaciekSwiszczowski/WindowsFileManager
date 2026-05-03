namespace WinUiFileManager.Presentation.FileEntryTable;

public sealed class FileEntryTableNavigationState
{
    public SpecFileEntryViewModel? CurrentItem { get; private set; }

    public int? CurrentIndex { get; private set; }

    public int? SelectionAnchorIndex { get; private set; }

    public int? SelectionCursorIndex { get; private set; }

    public void Reset()
    {
        CurrentItem = null;
        CurrentIndex = null;
        SelectionAnchorIndex = null;
        SelectionCursorIndex = null;
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

    public void SetCurrent(SpecFileEntryViewModel? item, int? index, bool resetSelectionAnchor)
    {
        CurrentItem = item;
        CurrentIndex = index;
        SelectionCursorIndex = index;

        if (resetSelectionAnchor)
        {
            SelectionAnchorIndex = index;
        }
    }

    public void SetSelectionRange(int anchorIndex, int cursorIndex)
    {
        SelectionAnchorIndex = anchorIndex;
        SelectionCursorIndex = cursorIndex;
        CurrentIndex = cursorIndex;
    }

    public int? GetCurrentIndex(TableView table)
    {
        if (ClampIndex(table, CurrentIndex) is { } currentIndex)
        {
            return currentIndex;
        }

        if (CurrentItem is not null && table.Items.IndexOf(CurrentItem) is var itemIndex && itemIndex >= 0)
        {
            return itemIndex;
        }

        return null;
    }

    public int? GetSelectionAnchorIndex(TableView table) => ClampIndex(table, SelectionAnchorIndex);

    public int? GetSelectionCursorIndex(TableView table) => ClampIndex(table, SelectionCursorIndex);

    public static int? ClampIndex(TableView table, int? index)
    {
        if (index is null || table.Items.Count == 0)
        {
            return null;
        }

        return Math.Clamp(index.Value, 0, table.Items.Count - 1);
    }
}
