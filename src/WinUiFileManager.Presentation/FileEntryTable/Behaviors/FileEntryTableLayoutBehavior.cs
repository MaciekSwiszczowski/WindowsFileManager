namespace WinUiFileManager.Presentation.FileEntryTable.Behaviors;

public sealed class FileEntryTableLayoutBehavior : FileEntryTableBehaviorBase
{
    protected override void OnLoaded(FileEntryTableBehaviorContext context) =>
        context.Messenger.Register<FileTableColumnLayoutMessage>(this, OnColumnLayoutMessage);

    private void OnColumnLayoutMessage(object recipient, FileTableColumnLayoutMessage message)
    {
        if (message.Identity != Context.View.Identity)
        {
            return;
        }

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
