using System.Reflection;

namespace WinUiFileManager.Presentation.FileEntryTable;

/// <summary>
/// Synchronizes private <see cref="TableView"/> keyboard-selection anchors after code changes selection.
/// </summary>
/// <remarks>
/// WinUI.TableView keeps arrow-key movement state in internal members. Native keyboard navigation is smooth
/// when those members match the visible selected row, but they can become stale after programmatic selection
/// changes. Reflection is intentionally isolated here so table behaviors can keep using the native keyboard
/// navigation engine without duplicating it or fighting it.
/// </remarks>
internal static class TableViewKeyboardAnchorSynchronizer
{
    private static readonly PropertyInfo? LastSelectionUnitProperty =
        typeof(TableView).GetProperty(
            "LastSelectionUnit",
            BindingFlags.NonPublic | BindingFlags.Instance);

    private static readonly PropertyInfo? CurrentRowIndexProperty =
        typeof(TableView).GetProperty(
            "CurrentRowIndex",
            BindingFlags.NonPublic | BindingFlags.Instance);

    private static readonly PropertyInfo? SelectionStartRowIndexProperty =
        typeof(TableView).GetProperty(
            "SelectionStartRowIndex",
            BindingFlags.NonPublic | BindingFlags.Instance);

    private static readonly PropertyInfo? SelectionStartCellSlotProperty =
        typeof(TableView).GetProperty(
            "SelectionStartCellSlot",
            BindingFlags.NonPublic | BindingFlags.Instance);

    /// <summary>
    /// Aligns native arrow-key movement and Shift-selection anchors with the supplied selected row index.
    /// </summary>
    /// <param name="table">The table whose private keyboard anchors should be synchronized.</param>
    /// <param name="rowIndex">The selected row index visible to the user.</param>
    public static void Sync(TableView table, int rowIndex)
    {
        try
        {
            LastSelectionUnitProperty?.SetValue(table, TableViewSelectionUnit.Row);
            CurrentRowIndexProperty?.SetValue(table, rowIndex);
            SelectionStartRowIndexProperty?.SetValue(table, rowIndex);
            SelectionStartCellSlotProperty?.SetValue(table, null);
        }
        catch
        {
            // If WinUI.TableView renames these internals, native behavior degrades to its default anchor logic.
        }
    }
}
