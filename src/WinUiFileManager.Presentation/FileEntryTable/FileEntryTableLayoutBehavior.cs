using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Xaml.Interactivity;
using WinUiFileManager.Presentation.FileEntryTable.Messages;

namespace WinUiFileManager.Presentation.FileEntryTable;

public sealed class FileEntryTableLayoutBehavior : Behavior<SpecFileEntryTableView>
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

        var table = AssociatedObject?.FindDescendant<TableView>();
        if (table is null)
        {
            return;
        }

        foreach (var column in table.Columns)
        {
            if (MapColumn(column.SortMemberPath) is { } fileEntryColumn)
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

    private static FileEntryColumn? MapColumn(string? sortMemberPath) =>
        sortMemberPath switch
        {
            nameof(SpecFileEntryViewModel.Name) => FileEntryColumn.Name,
            nameof(SpecFileEntryViewModel.Extension) => FileEntryColumn.Extension,
            nameof(SpecFileEntryViewModel.Size) => FileEntryColumn.Size,
            nameof(SpecFileEntryViewModel.Modified) => FileEntryColumn.Modified,
            nameof(SpecFileEntryViewModel.Attributes) => FileEntryColumn.Attributes,
            _ => null,
        };
}
