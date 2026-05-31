namespace WinUiFileManager.Presentation.FileEntryTable;

/// <summary>
/// Immutable set of file-table column pixel widths, shared between panes so their columns stay aligned.
/// Broadcast via <c>FileTableColumnLayoutMessage</c> and applied by <c>FileEntryTableLayoutBehavior</c>.
/// </summary>
public sealed record ColumnLayout(
    double NameWidth,
    double ExtensionWidth,
    double SizeWidth,
    double ModifiedWidth,
    double AttributesWidth)
{
    /// <summary>The default column widths applied before the user resizes anything.</summary>
    public static ColumnLayout Default { get; } = new(320, 40, 70, 120, 50);
}
