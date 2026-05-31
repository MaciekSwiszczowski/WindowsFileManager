namespace WinUiFileManager.Presentation.FileEntryTable.Behaviors;

/// <summary>
/// Applies an externally-supplied <see cref="ColumnLayout"/> (column widths) to this pane's table when
/// a <see cref="FileTableColumnLayoutMessage"/> arrives, keeping the two panes' column widths in sync.
/// </summary>
/// <remarks>
/// Pane-scoped via <c>IdentityFilter.For&lt;FileTableColumnLayoutMessage&gt;</c> on the view identity
/// (AGENTS.md §4). Registration-only behavior — there are no UI event subscriptions to reverse, so it
/// relies entirely on the base class's <c>UnregisterAll</c> for cleanup and does not override
/// <c>OnUnloaded</c>.
/// </remarks>
public sealed class FileEntryTableLayoutBehavior : FileEntryTableBehaviorBase
{
    protected override void OnLoaded(FileEntryTableContext context) =>
        context.Messenger.Register(
            this,
            IdentityFilter.For<FileTableColumnLayoutMessage>(context.View.Identity, OnColumnLayoutMessage));

    private void OnColumnLayoutMessage(FileTableColumnLayoutMessage message)
    {
        foreach (var column in Context.Table.Columns)
        {
            if (FileEntryTableColumnMapping.MapColumn(column.SortMemberPath) is { } fileEntryColumn)
            {
                column.Width = new GridLength(MapWidth(fileEntryColumn, message.Layout));
            }
        }
    }

    /// <summary>Maps a logical file-entry column to its pixel width from the supplied layout; falls
    /// back to the name width for any unexpected column.</summary>
    private static double MapWidth(SortColumn column, ColumnLayout layout) =>
        column switch
        {
            SortColumn.Name => layout.NameWidth,
            SortColumn.Extension => layout.ExtensionWidth,
            SortColumn.Size => layout.SizeWidth,
            SortColumn.Modified => layout.ModifiedWidth,
            SortColumn.Attributes => layout.AttributesWidth,
            _ => layout.NameWidth,
        };
}
