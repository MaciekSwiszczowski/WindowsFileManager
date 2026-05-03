namespace WinUiFileManager.Presentation.FileEntryTable.Behaviors;

public sealed class FileEntryTableLayoutBehavior : FileEntryTableBehavior
{
    protected override void OnAttached()
    {
        base.OnAttached();
        WeakReferenceMessenger.Default.Register<FileTableColumnLayoutMessage>(this, OnColumnLayoutMessage);
    }

    protected override void OnDetaching()
    {
        WeakReferenceMessenger.Default.Unregister<FileTableColumnLayoutMessage>(this);
        base.OnDetaching();
    }

    private void OnColumnLayoutMessage(object recipient, FileTableColumnLayoutMessage message)
    {
        if (AssociatedObject is null || message.Identity != AssociatedObject.Identity)
        {
            return;
        }

        foreach (var column in AssociatedObject.Table.Columns)
        {
            if (FileEntryTableBehaviorHelper.MapColumn(column.SortMemberPath) is { } fileEntryColumn)
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
