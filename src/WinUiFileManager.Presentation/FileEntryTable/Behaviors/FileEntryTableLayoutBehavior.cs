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

    private static double MapWidth(FileEntryColumn column, ColumnLayout layout) =>
        column switch
        {
            FileEntryColumn.Name => layout.NameWidth,
            FileEntryColumn.Extension => layout.ExtensionWidth,
            FileEntryColumn.Size => layout.SizeWidth,
            FileEntryColumn.Modified => layout.ModifiedWidth,
            FileEntryColumn.Attributes => layout.AttributesWidth,
            _ => layout.NameWidth,
        };
}
