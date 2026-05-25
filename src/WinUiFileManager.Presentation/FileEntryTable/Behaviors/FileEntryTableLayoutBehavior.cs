namespace WinUiFileManager.Presentation.FileEntryTable.Behaviors;

public sealed class FileEntryTableLayoutBehavior : FileEntryTableBehaviorBase
{
    protected override void OnLoaded(FileEntryTableContext context) =>
        context.Messenger.Register(
            this,
            MessageIdentity.Filter<FileTableColumnLayoutMessage>(context.View.Identity, OnColumnLayoutMessage));

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
